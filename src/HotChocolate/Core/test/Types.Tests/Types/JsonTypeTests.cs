using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CookieCrumble;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Types;

public class JsonTypeTests
{
    [Fact]
    public async Task Json_Schema()
    {
        var schema =
            await new ServiceCollection()
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .BuildSchemaAsync();

        schema.MatchInlineSnapshot(
            """
            schema {
              query: Query
            }

            type Query {
              someJson: JSON!
              manyJson: [JSON!]
              inputJson(input: JSON!): JSON!
              jsonFromString: JSON!
            }

            scalar JSON
            """);
    }

    [Fact]
    public async Task Output_Json_Object()
    {
        var result =
            await new ServiceCollection()
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .ExecuteRequestAsync(
                    """
                    {
                        someJson
                    }
                    """);

        result.MatchInlineSnapshot(
            """
            {
              "data": {
                "someJson": {
                  "a": {
                    "b": 123.456
                  }
                }
              }
            }
            """);
    }

    [Fact]
    public async Task Output_Json_Object_List()
    {
        var result =
            await new ServiceCollection()
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .ExecuteRequestAsync(
                    """
                    {
                        manyJson
                    }
                    """);

        result.MatchInlineSnapshot(
            """
            {
              "data": {
                "manyJson": [
                  {
                    "a": {
                      "b": 123.456
                    }
                  },
                  {
                    "x": {
                      "y": "y"
                    }
                  }
                ]
              }
            }
            """);
    }

    [Fact]
    public async Task Input_Json_Object_Literal()
    {
        var result =
            await new ServiceCollection()
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .ExecuteRequestAsync(
                    """
                    {
                        inputJson(input: { a: "abc" })
                    }
                    """);

        result.MatchInlineSnapshot(
            """
            {
              "data": {
                "inputJson": {
                  "a": "abc"
                }
              }
            }
            """);
    }

    [Fact]
    public async Task Input_Json_Object_Variables()
    {
        var input = JsonDocument.Parse(
            """
            {
              "a": {
                "b": 123.456
              }
            }
            """).RootElement;

        var result =
            await new ServiceCollection()
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .ExecuteRequestAsync(
                    QueryRequestBuilder.New()
                        .SetQuery(
                            """
                            query($input: JSON!) {
                                inputJson(input: $input)
                            }
                            """)
                        .SetVariableValue("input", input)
                        .Create());

        result.MatchInlineSnapshot(
            """
            {
              "data": {
                "inputJson": {
                  "a": {
                    "b": 123.456
                  }
                }
              }
            }
            """);
    }

    [Fact]
    public async Task Output_Json_From_String()
    {
        var result =
            await new ServiceCollection()
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .ExecuteRequestAsync(
                    """
                    {
                        jsonFromString
                    }
                    """);

        result.MatchInlineSnapshot(
            """
            {
              "data": {
                "jsonFromString": {
                  "a": "b"
                }
              }
            }
            """);
    }

    public class Query
    {
        public JsonElement GetSomeJson()
            => JsonDocument.Parse(
                """
                {
                  "a": {
                    "b": 123.456
                  }
                }
                """).RootElement;

        public IEnumerable<JsonElement> GetManyJson()
        {
            yield return JsonDocument.Parse(
                """
                {
                  "a": {
                    "b": 123.456
                  }
                }
                """).RootElement;

            yield return JsonDocument.Parse(
                """
                {
                  "x": {
                    "y": "y"
                  }
                }
                """).RootElement;
        }

        public JsonElement InputJson(JsonElement input)
            => input;

        [GraphQLType<NonNullType<JsonType>>]
        public string JsonFromString()
            => "{ \"a\": \"b\" }";
    }
}