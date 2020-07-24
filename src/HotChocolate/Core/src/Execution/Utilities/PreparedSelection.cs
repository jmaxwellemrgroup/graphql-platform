using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HotChocolate.Execution.Properties;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace HotChocolate.Execution.Utilities
{
    public sealed class PreparedSelection : IPreparedSelection
    {
        private static readonly PreparedArgumentMap _emptyArguments =
            new PreparedArgumentMap(new Dictionary<NameString, PreparedArgument>());

        private static readonly IReadOnlyList<FieldNode> _emptySelections = new FieldNode[0];
        private List<SelectionIncludeCondition>? _includeConditions;
        private List<FieldNode>? _selections;
        private bool _isReadOnly;

        public PreparedSelection(
            IObjectType declaringType,
            IObjectField field,
            FieldNode selection,
            FieldDelegate resolverPipeline,
            NameString? responseName = null,
            IReadOnlyDictionary<NameString, PreparedArgument>? arguments = null,
            SelectionIncludeCondition? includeCondition = null,
            bool internalSelection = false)
        {
            DeclaringType = declaringType
                ?? throw new ArgumentNullException(nameof(declaringType));
            Field = field
                ?? throw new ArgumentNullException(nameof(field));
            Selection = selection
                ?? throw new ArgumentNullException(nameof(selection));
            ResponseName = responseName ??
                (selection.Alias is null
                    ? selection.Name.Value
                    : selection.Alias.Value);
            ResolverPipeline = resolverPipeline ??
                throw new ArgumentNullException(nameof(resolverPipeline));
            Arguments = arguments is null
                ? _emptyArguments
                : new PreparedArgumentMap(arguments);
            Selections = _emptySelections;
            InclusionKind = internalSelection
                ? SelectionInclusionKind.Internal
                : SelectionInclusionKind.Always;

            if (includeCondition is { })
            {
                _includeConditions = new List<SelectionIncludeCondition> { includeCondition };
                ModifyCondition(true);
            }
        }

        /// <inheritdoc />
        public IObjectType DeclaringType { get; }

        /// <inheritdoc />
        public IObjectField Field { get; }

        /// <inheritdoc />
        public FieldNode Selection { get; private set; }

        public SelectionSetNode? SelectionSet => Selection.SelectionSet;

        /// <inheritdoc />
        public IReadOnlyList<FieldNode> Selections { get; private set; }

        IReadOnlyList<FieldNode> IFieldSelection.Nodes => Selections;

        /// <inheritdoc />
        public NameString ResponseName { get; }

        /// <inheritdoc />
        public FieldDelegate ResolverPipeline { get; }

        /// <inheritdoc />
        public IPreparedArgumentMap Arguments { get; }

        /// <inheritdoc />
        public SelectionInclusionKind InclusionKind { get; private set; }

        public bool IsInternal =>
            InclusionKind == SelectionInclusionKind.Internal ||
            InclusionKind == SelectionInclusionKind.InternalConditional;

        public bool IsConditional =>
            InclusionKind == SelectionInclusionKind.Conditional ||
            InclusionKind == SelectionInclusionKind.InternalConditional;

        internal IReadOnlyList<SelectionIncludeCondition>? IncludeConditions => _includeConditions;

        /// <inheritdoc />
        public bool IsIncluded(IVariableValueCollection variableValues, bool allowInternals = false)
        {
            return InclusionKind switch
            {
                SelectionInclusionKind.Always => true,
                SelectionInclusionKind.Conditional => EvaluateConditions(variableValues),
                SelectionInclusionKind.Internal => allowInternals,
                SelectionInclusionKind.InternalConditional =>
                    allowInternals && EvaluateConditions(variableValues),
                _ => throw new NotSupportedException()
            };
        }

        private bool EvaluateConditions(IVariableValueCollection variableValues)
        {
            Debug.Assert(
                _includeConditions != null,
                "If a selection is conditional it must have visibility conditions.");

            for (var i = 0; i < _includeConditions!.Count; i++)
            {
                if (_includeConditions[i].IsTrue(variableValues))
                {
                    return true;
                }
            }

            return false;
        }

        public void AddSelection(FieldNode field, SelectionIncludeCondition? includeCondition)
        {
            if (_isReadOnly)
            {
                throw new NotSupportedException(Resources.PreparedSelection_ReadOnly);
            }

            _selections ??= new List<FieldNode> { Selection };
            _selections.Add(field);

            AddVariableVisibility(includeCondition);
        }

        private void AddVariableVisibility(SelectionIncludeCondition? includeCondition)
        {
            if (_isReadOnly)
            {
                throw new NotSupportedException(Resources.PreparedSelection_ReadOnly);
            }

            if (_includeConditions is null)
            {
                return;
            }

            if (includeCondition is null)
            {
                _includeConditions = null;
                ModifyCondition(false);
                return;
            }

            for (var i = 0; i < _includeConditions.Count; i++)
            {
                if (_includeConditions[i].Equals(includeCondition))
                {
                    return;
                }
            }

            _includeConditions.Add(includeCondition);
        }

        internal void MakeReadOnly()
        {
            if (_isReadOnly)
            {
                return;
            }

            _isReadOnly = true;
            Selection = MergeField(Selection, _selections);
            Selections = _selections ?? _emptySelections;
        }

        private static FieldNode MergeField(
            FieldNode first,
            IReadOnlyList<FieldNode>? selections)
        {
            if (selections is null)
            {
                return first;
            }

            return new FieldNode
            (
                first.Location,
                first.Name,
                first.Alias,
                MergeDirectives(selections),
                first.Arguments,
                MergeSelections(first, selections)
            );
        }

        private static SelectionSetNode? MergeSelections(
            FieldNode first,
            IReadOnlyList<FieldNode> selections)
        {
            if (first.SelectionSet is null)
            {
                return null;
            }

            var children = new List<ISelectionNode>();

            for (var i = 0; i < selections.Count; i++)
            {
                if (selections[i].SelectionSet is { } selectionSet)
                {
                    children.AddRange(selectionSet.Selections);
                }
            }

            return new SelectionSetNode
            (
                selections[0].SelectionSet!.Location,
                children
            );
        }

        private static IReadOnlyList<DirectiveNode> MergeDirectives(
            IReadOnlyList<FieldNode> selections)
        {
            var firstWithDirectives = -1;
            List<DirectiveNode>? merged = null;

            for (var i = 0; i < selections.Count; i++)
            {
                FieldNode selection = selections[i];
                if (selection.Directives.Count > 0)
                {
                    if (firstWithDirectives == -1)
                    {
                        firstWithDirectives = i;
                    }
                    else if (merged is null)
                    {
                        merged = selections[firstWithDirectives].Directives.ToList();
                        merged.AddRange(selection.Directives);
                    }
                    else
                    {
                        merged.AddRange(selection.Directives);
                    }
                }
            }

            if (merged is { })
            {
                return merged;
            }

            if (firstWithDirectives != -1)
            {
                return selections[firstWithDirectives].Directives;
            }

            return selections[0].Directives;
        }

        private void ModifyCondition(bool hasConditions) =>
            InclusionKind =
                (InclusionKind == SelectionInclusionKind.Internal
                || InclusionKind == SelectionInclusionKind.InternalConditional)
                    ? (hasConditions
                        ? SelectionInclusionKind.InternalConditional
                        : SelectionInclusionKind.Internal)
                    : (hasConditions
                        ? SelectionInclusionKind.Conditional
                        : SelectionInclusionKind.Always);
    }
}
