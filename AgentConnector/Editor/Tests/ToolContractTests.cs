using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ToolContractTests
    {
        private static readonly Dictionary<string, JObject> MinimumInputs =
            new Dictionary<string, JObject>(StringComparer.Ordinal)
            {
                ["console"] = new JObject(),
                ["describe_shader"] = new JObject { ["list"] = true },
                ["describe_type"] = new JObject { ["type"] = "UnityEngine.GameObject" },
                ["find_gameobjects"] = new JObject(),
                ["find_method"] = new JObject { ["pattern"] = "Refresh" },
                ["game_feel"] = new JObject(),
                ["list_assemblies"] = new JObject(),
                ["ui_slop"] = new JObject(),
                ["unity_docs"] = new JObject { ["query"] = "GameObject" },
            };

        [MenuItem("HeraAgent/Tests/ToolContract")]
        public static void RunTests()
        {
            var allPassed = true;

            allPassed &= TestMinimumValidInput();
            allPassed &= TestM21RuntimeOutputEnvelopes();
            allPassed &= TestStrictSchemaRejectsUnknownProperty();
            allPassed &= TestWrongTypesAreRejected();
            allPassed &= TestExplicitNullRequiresAllowNull();
            allPassed &= TestMissingRequiredArguments();
            allPassed &= TestAliasesNormalizeBeforeValidation();
            allPassed &= TestDeprecatedAliasReturnsDiagnostic();
            allPassed &= TestSchemaConstraintsAndInvalidFragments();
            allPassed &= TestM21ValueConstraints();
            allPassed &= TestCrossFieldConstraints();
            allPassed &= TestActionSchemaUsesConstOrEnum();
            allPassed &= TestClassLevelActionContractsAreListed();
            allPassed &= TestEveryM21ActionDispatches();
            allPassed &= TestActionValidationFailures();
            allPassed &= TestUnknownActionReturnsStableError();
            allPassed &= TestMutuallyExclusiveTargets();
            allPassed &= TestDescribeShaderAlternatives();
            allPassed &= TestOutputSchemasHaveEnvelopeShape();
            allPassed &= TestM22StrictToolCoverage();
            allPassed &= TestEveryM22ActionContract();
            allPassed &= TestM22ValidationFailures();
            allPassed &= TestM22AliasesNormalize();
            allPassed &= TestM22MutuallyExclusiveTargets();
            allPassed &= TestM22ComplexSchemaValues();
            allPassed &= TestM22ScalarCompatibility();
            allPassed &= TestM22OutputSchemas();
            allPassed &= TestM23StrictToolCoverage();
            allPassed &= TestEveryM23ActionContract();
            allPassed &= TestM23ValidationFailures();
            allPassed &= TestM23AliasesNormalize();
            allPassed &= TestM23MutuallyExclusiveTargets();
            allPassed &= TestM23ComplexSchemaValues();
            allPassed &= TestM23OutputSchemas();
            allPassed &= TestM24StrictToolCoverage();
            allPassed &= TestEveryM24ActionContract();
            allPassed &= TestM24ValidationFailures();
            allPassed &= TestM24AliasesNormalize();
            allPassed &= TestM24MutuallyExclusiveTargets();
            allPassed &= TestM24OutputSchemas();
            allPassed &= TestM8RestrictedExecContract();

            if (allPassed)
                Debug.Log("[ToolContractTests] ALL PASSED");
            else
                Debug.LogError("[ToolContractTests] SOME TESTS FAILED");
        }

        private static bool TestMinimumValidInput()
        {
            foreach (var entry in MinimumInputs)
            {
                var contract = ToolContractRegistry.Get(entry.Key);
                var result = ToolContractValidator.Validate(contract, entry.Value);
                if (contract?.Mode != ToolContractMode.Strict
                    || !result.IsValid
                    || contract.InputSchema["additionalProperties"]?.Value<bool>() != false)
                    return Expect(nameof(TestMinimumValidInput), false);
            }

            return Expect(nameof(TestMinimumValidInput), true);
        }

        private static bool TestStrictSchemaRejectsUnknownProperty()
        {
            foreach (var entry in MinimumInputs)
            {
                var input = (JObject)entry.Value.DeepClone();
                input["unknown_m21_property"] = true;
                var result = ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Key),
                    input);
                if (result.Error?.code != "UNKNOWN_ARGUMENT")
                    return Expect(nameof(TestStrictSchemaRejectsUnknownProperty), false);
            }

            foreach (var entry in StrictActions())
            {
                var input = new JObject
                {
                    ["action"] = entry.action,
                    ["unknown_m21_property"] = true,
                };
                var result = ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.tool),
                    input,
                    entry.action);
                if (result.Error?.code != "UNKNOWN_ARGUMENT")
                    return Expect(nameof(TestStrictSchemaRejectsUnknownProperty), false);
            }

            return Expect(nameof(TestStrictSchemaRejectsUnknownProperty), true);
        }

        private static bool TestM21RuntimeOutputEnvelopes()
        {
            foreach (var entry in MinimumInputs)
            {
                var response = CommandRouter.Dispatch(entry.Key, entry.Value)
                    .GetAwaiter()
                    .GetResult();
                if (!HasRuntimeEnvelope(response))
                    return Expect(nameof(TestM21RuntimeOutputEnvelopes), false);
            }

            foreach (var entry in StrictActions())
            {
                var response = CommandRouter.Dispatch(
                        entry.tool,
                        new JObject { ["action"] = entry.action })
                    .GetAwaiter()
                    .GetResult();
                if (!HasRuntimeEnvelope(response))
                    return Expect(nameof(TestM21RuntimeOutputEnvelopes), false);
            }

            return Expect(nameof(TestM21RuntimeOutputEnvelopes), true);
        }

        private static bool HasRuntimeEnvelope(object response)
        {
            if (!(response is SuccessResponse) && !(response is ErrorResponse))
                return false;
            var envelope = JObject.FromObject(response);
            if (envelope["message"]?.Type != JTokenType.String)
                return false;
            if (response is ErrorResponse)
                return envelope.Value<bool>("success") == false
                    && envelope["code"]?.Type == JTokenType.String;
            return envelope.Value<bool>("success")
                && envelope["data"] != null
                && envelope["data"].Type != JTokenType.Null;
        }

        private static bool TestWrongTypesAreRejected()
        {
            var cases = new[]
            {
                ("console", new JObject { ["lines"] = new JObject() }),
                ("describe_shader", new JObject { ["list"] = new JObject() }),
                ("describe_type", new JObject
                {
                    ["type"] = "UnityEngine.GameObject",
                    ["limit"] = new JObject(),
                }),
                ("find_gameobjects", new JObject { ["offset"] = new JObject() }),
                ("find_method", new JObject
                {
                    ["pattern"] = "Refresh",
                    ["limit"] = new JObject(),
                }),
                ("game_feel", new JObject { ["topic"] = new JObject() }),
                ("list_assemblies", new JObject { ["include_system"] = new JObject() }),
                ("ui_slop", new JObject { ["id"] = new JObject() }),
                ("unity_docs", new JObject { ["query"] = new JObject() }),
            };

            var valid = cases.All(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item2).Error?.code == "ARGUMENT_TYPE_MISMATCH");
            return Expect(nameof(TestWrongTypesAreRejected), valid);
        }

        private static bool TestMissingRequiredArguments()
        {
            var tools = new[] { "describe_type", "find_method", "unity_docs" };
            var valid = tools.All(tool =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(tool),
                    new JObject()).Error?.code == "MISSING_ARGUMENT");
            return Expect(nameof(TestMissingRequiredArguments), valid);
        }

        private static bool TestExplicitNullRequiresAllowNull()
        {
            var rejected = ToolContractValidator.Validate(
                ToolContractRegistry.Get("console"),
                new JObject { ["lines"] = JValue.CreateNull() });
            var allowed = ToolContractValidator.Validate(
                ToolContractRegistry.Build(typeof(NullableFixture)),
                new JObject { ["value"] = JValue.CreateNull() });
            return Expect(nameof(TestExplicitNullRequiresAllowNull),
                rejected.Error?.code == "ARGUMENT_TYPE_MISMATCH"
                && allowed.IsValid);
        }

        private static bool TestAliasesNormalizeBeforeValidation()
        {
            var result = ToolContractValidator.Validate(
                ToolContractRegistry.Get("console"),
                new JObject { ["count"] = "7" });
            return Expect(nameof(TestAliasesNormalizeBeforeValidation),
                result.IsValid
                && result.Normalized["count"] == null
                && result.Normalized.Value<int>("lines") == 7);
        }

        private static bool TestDeprecatedAliasReturnsDiagnostic()
        {
            var contract = ToolContractRegistry.Build(typeof(DeprecatedAliasFixture));
            var result = ToolContractValidator.Validate(
                contract,
                new JObject { ["old_value"] = "legacy" });
            return Expect(nameof(TestDeprecatedAliasReturnsDiagnostic),
                result.IsValid
                && result.Normalized.Value<string>("value") == "legacy"
                && result.Diagnostics.Count == 1
                && result.Diagnostics[0].Path == "/old_value");
        }

        private static bool TestSchemaConstraintsAndInvalidFragments()
        {
            var contract = ToolContractRegistry.Build(typeof(DeprecatedAliasFixture));
            var enumFailure = ToolContractValidator.Validate(
                contract,
                new JObject { ["mode"] = "other" });
            var formatFailure = ToolContractValidator.Validate(
                contract,
                new JObject { ["uri"] = "relative/path" });
            var valid = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["mode"] = "first",
                    ["uri"] = "https://example.com",
                });

            var invalidFragmentFailed = false;
            try
            {
                ToolContractRegistry.Build(typeof(InvalidSchemaFixture));
            }
            catch (SchemaGenerationException)
            {
                invalidFragmentFailed = true;
            }

            var invalidKeywordFailed = false;
            try
            {
                ToolContractRegistry.Build(typeof(InvalidKeywordSchemaFixture));
            }
            catch (SchemaGenerationException)
            {
                invalidKeywordFailed = true;
            }

            return Expect(nameof(TestSchemaConstraintsAndInvalidFragments),
                enumFailure.Error?.code == "INVALID_ARGUMENT"
                && formatFailure.Error?.code == "ARGUMENT_FORMAT_INVALID"
                && valid.IsValid
                && invalidFragmentFailed
                && invalidKeywordFailed);
        }

        private static bool TestM21ValueConstraints()
        {
            var invalidMembers = ToolContractValidator.Validate(
                ToolContractRegistry.Get("describe_type"),
                new JObject
                {
                    ["type"] = "UnityEngine.GameObject",
                    ["members"] = "bogus",
                });
            var invalidStacktrace = ToolContractValidator.Validate(
                ToolContractRegistry.Get("console"),
                new JObject { ["stacktrace"] = "verbose" });
            var invalidTypes = ToolContractValidator.Validate(
                ToolContractRegistry.Get("console"),
                new JObject { ["type"] = "error,banana" });
            return Expect(nameof(TestM21ValueConstraints),
                invalidMembers.Error?.code == "INVALID_ARGUMENT"
                && invalidStacktrace.Error?.code == "INVALID_ARGUMENT"
                && invalidTypes.Error?.code == "INVALID_ARGUMENT");
        }

        private static bool TestCrossFieldConstraints()
        {
            var shader = ToolContractRegistry.Get("describe_shader");
            var shaderConflict = ToolContractValidator.Validate(
                shader,
                new JObject
                {
                    ["name"] = "Standard",
                    ["list"] = true,
                });
            var shaderMissing = ToolContractValidator.Validate(shader, new JObject());

            var projection = ToolContractRegistry.Get("find_gameobjects");
            var projectionConflict = ToolContractValidator.Validate(
                projection,
                new JObject
                {
                    ["ids"] = true,
                    ["names"] = true,
                });

            return Expect(nameof(TestCrossFieldConstraints),
                shaderConflict.Error?.code == "ARGUMENT_CONFLICT"
                && shaderMissing.Error?.code == "MISSING_ARGUMENT"
                && projectionConflict.Error?.code == "INVALID_PROJECTION"
                && shader.InputSchema["oneOf"] is JArray
                && projection.InputSchema["allOf"] is JArray);
        }

        private static bool TestActionSchemaUsesConstOrEnum()
        {
            foreach (var entry in StrictActions())
            {
                var contract = ToolContractRegistry.Get(entry.tool);
                if (!contract.Actions.TryGetValue(entry.action, out var action))
                    return Expect(nameof(TestActionSchemaUsesConstOrEnum), false);

                var actionSchema = action.InputSchema["properties"]?["action"] as JObject;
                if (actionSchema?["const"]?.Value<string>() != entry.action
                    || action.InputSchema["additionalProperties"]?.Value<bool>() != false)
                {
                    return Expect(nameof(TestActionSchemaUsesConstOrEnum), false);
                }
            }

            return Expect(nameof(TestActionSchemaUsesConstOrEnum), true);
        }

        private static bool TestUnknownActionReturnsStableError()
        {
            var cases = new[]
            {
                "scene",
                "menu",
                "manage_gameobject",
                "manage_components",
                "manage_editor",
                "input",
            };
            foreach (var tool in cases)
            {
                var response = CommandRouter.Dispatch(
                    tool,
                    new JObject { ["action"] = "not_a_real_action" })
                    .GetAwaiter()
                    .GetResult() as ErrorResponse;
                var data = response?.data == null ? null : JObject.FromObject(response.data);
                if (response?.code != "UNKNOWN_ACTION"
                    || data?.Value<string>("path") != "/action")
                {
                    return Expect(nameof(TestUnknownActionReturnsStableError), false);
                }
            }

            return Expect(nameof(TestUnknownActionReturnsStableError), true);
        }

        private static bool TestClassLevelActionContractsAreListed()
        {
            var contract = ToolContractRegistry.Build(typeof(ClassActionFixture));
            var descriptors = JArray.FromObject(
                ToolDiscovery.BuildActionDescriptors(
                    contract,
                    new Dictionary<string, string>(StringComparer.Ordinal)));
            return Expect(nameof(TestClassLevelActionContractsAreListed),
                descriptors.Count == 1
                && descriptors[0]?["name"]?.Value<string>() == "inspect"
                && descriptors[0]?["input_schema"] != null);
        }

        private static bool TestEveryM21ActionDispatches()
        {
            foreach (var entry in StrictActions())
            {
                var response = CommandRouter.Dispatch(
                    entry.tool,
                    new JObject { ["action"] = entry.action })
                    .GetAwaiter()
                    .GetResult();
                if (!(response is SuccessResponse))
                    return Expect(nameof(TestEveryM21ActionDispatches), false);
            }

            return Expect(nameof(TestEveryM21ActionDispatches), true);
        }

        private static bool TestActionValidationFailures()
        {
            var sceneContract = ToolContractRegistry.Get("scene");
            var missing = ToolContractValidator.Validate(
                sceneContract,
                new JObject(),
                "info");
            var wrongType = ToolContractValidator.Validate(
                ToolContractRegistry.Get("menu"),
                new JObject
                {
                    ["action"] = "list",
                    ["limit"] = new JObject(),
                },
                "list");
            return Expect(nameof(TestActionValidationFailures),
                missing.Error?.code == "MISSING_ARGUMENT"
                && wrongType.Error?.code == "ARGUMENT_TYPE_MISMATCH");
        }

        private static bool TestMutuallyExclusiveTargets()
        {
            var response = Tools.FindGameObjects.HandleCommand(new JObject
            {
                ["ids"] = true,
                ["names"] = true,
            }) as ErrorResponse;
            return Expect(nameof(TestMutuallyExclusiveTargets),
                response?.code == "INVALID_PROJECTION");
        }

        private static bool TestDescribeShaderAlternatives()
        {
            var missing = Tools.DescribeShader.HandleCommand(new JObject()) as ErrorResponse;
            var conflict = Tools.DescribeShader.HandleCommand(new JObject
            {
                ["name"] = "Standard",
                ["list"] = true,
            }) as ErrorResponse;
            return Expect(nameof(TestDescribeShaderAlternatives),
                missing?.code == "MISSING_ARGUMENT"
                && conflict?.code == "ARGUMENT_CONFLICT");
        }

        private static bool TestOutputSchemasHaveEnvelopeShape()
        {
            foreach (var toolName in MinimumInputs.Keys.Concat(new[] { "scene", "menu" }))
            {
                var tool = JObject.FromObject(ToolDiscovery.GetToolSchema(toolName));
                var properties = tool["output_schema"]?["properties"] as JObject;
                if (properties?["success"] == null
                    || properties["message"] == null
                    || properties["data"] == null)
                {
                    return Expect(nameof(TestOutputSchemasHaveEnvelopeShape), false);
                }
            }

            var scene = JObject.FromObject(ToolDiscovery.GetToolSchema("scene"));
            var actions = scene["actions"] as JArray;
            var info = actions?.FirstOrDefault(action =>
                action?["name"]?.Value<string>() == "info");
            var list = actions?.FirstOrDefault(action =>
                action?["name"]?.Value<string>() == "list");
            return Expect(nameof(TestOutputSchemasHaveEnvelopeShape),
                info?["output_schema"]?["properties"]?["data"]?["properties"]?["active"] != null
                && list?["output_schema"]?["properties"]?["data"]?["type"]?.Value<string>()
                    == "array");
        }

        private static bool TestM22StrictToolCoverage()
        {
            var tools = new[]
            {
                "manage_gameobject",
                "manage_components",
                "manage_editor",
                "screenshot",
                "input",
            };
            var screenshotMinimum = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject());
            var screenshotOverlay = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject { ["overlay"] = true });
            return Expect(nameof(TestM22StrictToolCoverage),
                screenshotMinimum.IsValid
                && screenshotOverlay.IsValid
                && tools.All(tool =>
            {
                var contract = ToolContractRegistry.Get(tool);
                return contract?.Mode == ToolContractMode.Strict
                    && contract.InputSchema["additionalProperties"]?.Value<bool>() == false;
            }));
        }

        private static bool TestEveryM22ActionContract()
        {
            foreach (var entry in StrictM22Actions())
            {
                var contract = ToolContractRegistry.Get(entry.tool);
                if (contract == null
                    || !contract.Actions.TryGetValue(entry.action, out var action)
                    || !action.IsStrict)
                {
                    return Expect(nameof(TestEveryM22ActionContract), false);
                }

                var validation = ToolContractValidator.Validate(
                    contract,
                    entry.input,
                    entry.action);
                if (!validation.IsValid
                    || action.InputSchema["properties"]?["action"]?["const"]?.Value<string>()
                        != entry.action)
                {
                    return Expect(nameof(TestEveryM22ActionContract), false);
                }

                if (entry.tool == "input"
                    && (entry.action == "keyboard"
                        || entry.action == "mouse"
                        || entry.action == "sequence"
                        || entry.action == "replay")
                    && (!action.Safety.RequiresPlayMode
                        || action.Safety.RiskClass != HeraRiskClass.Write))
                {
                    return Expect(nameof(TestEveryM22ActionContract), false);
                }
            }

            return Expect(nameof(TestEveryM22ActionContract), true);
        }

        private static bool TestM22ValidationFailures()
        {
            var missingCases = new[]
            {
                ("scene", "close", new JObject { ["action"] = "close" }),
                ("manage_gameobject", "move", new JObject
                {
                    ["action"] = "move",
                    ["instance_id"] = 1,
                }),
                ("manage_components", "add", new JObject
                {
                    ["action"] = "add",
                    ["instance_id"] = 1,
                }),
                ("manage_components", "get", new JObject
                {
                    ["action"] = "get",
                    ["path"] = "/Root",
                }),
                ("manage_editor", "add_tag", new JObject { ["action"] = "add_tag" }),
                ("input", "inspect", new JObject { ["action"] = "inspect" }),
            };
            if (missingCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "MISSING_ARGUMENT"))
            {
                return Expect(nameof(TestM22ValidationFailures), false);
            }

            var wrongTypeCases = new[]
            {
                ("scene", "load", new JObject
                {
                    ["action"] = "load",
                    ["path"] = new JObject(),
                }),
                ("manage_gameobject", "set_active", new JObject
                {
                    ["action"] = "set_active",
                    ["instance_id"] = 1,
                    ["active"] = new JObject(),
                }),
                ("manage_components", "list", new JObject
                {
                    ["action"] = "list",
                    ["instance_id"] = new JObject(),
                }),
                ("manage_editor", "set_active_tool", new JObject
                {
                    ["action"] = "set_active_tool",
                    ["tool_name"] = new JObject(),
                }),
                ("input", "state", new JObject
                {
                    ["action"] = "state",
                    ["max_results"] = new JObject(),
                }),
            };
            if (wrongTypeCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "ARGUMENT_TYPE_MISMATCH"))
            {
                return Expect(nameof(TestM22ValidationFailures), false);
            }

            foreach (var entry in StrictM22Actions())
            {
                var unknown = (JObject)entry.input.DeepClone();
                unknown["unknown_m22_property"] = true;
                if (ToolContractValidator.Validate(
                        ToolContractRegistry.Get(entry.tool),
                        unknown,
                        entry.action).Error?.code != "UNKNOWN_ARGUMENT")
                {
                    return Expect(nameof(TestM22ValidationFailures), false);
                }
            }

            var screenshotWrongType = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject { ["width"] = new JObject() });
            var screenshotUnknown = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject { ["unknown_m22_property"] = true });
            var screenshotMissingTarget = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject { ["isolated"] = true });
            return Expect(nameof(TestM22ValidationFailures),
                screenshotWrongType.Error?.code == "ARGUMENT_TYPE_MISMATCH"
                && screenshotUnknown.Error?.code == "UNKNOWN_ARGUMENT"
                && screenshotMissingTarget.Error?.code == "MISSING_ARGUMENT");
        }

        private static bool TestM22AliasesNormalize()
        {
            var scene = ToolContractValidator.Validate(
                ToolContractRegistry.Get("scene"),
                new JObject
                {
                    ["action"] = "load",
                    ["name"] = "SampleScene",
                },
                "load");
            var sceneTarget = ToolContractValidator.Validate(
                ToolContractRegistry.Get("scene"),
                new JObject
                {
                    ["action"] = "close",
                    ["target"] = "SampleScene",
                },
                "close");
            var scroll = ToolContractValidator.Validate(
                ToolContractRegistry.Get("input"),
                new JObject
                {
                    ["action"] = "scroll",
                    ["path"] = "/Canvas/Button",
                    ["delta"] = "0,-1",
                },
                "scroll");
            var drag = ToolContractValidator.Validate(
                ToolContractRegistry.Get("input"),
                new JObject
                {
                    ["action"] = "drag",
                    ["path"] = "/Canvas/Slider",
                    ["to"] = "10,20",
                },
                "drag");
            return Expect(nameof(TestM22AliasesNormalize),
                scene.IsValid
                && scene.Normalized.Value<string>("path") == "SampleScene"
                && scene.Normalized["name"] == null
                && sceneTarget.IsValid
                && sceneTarget.Normalized.Value<string>("path") == "SampleScene"
                && sceneTarget.Normalized["target"] == null
                && scroll.IsValid
                && scroll.Normalized.Value<string>("scroll_delta") == "0,-1"
                && scroll.Normalized["delta"] == null
                && drag.IsValid
                && drag.Normalized.Value<string>("to_position") == "10,20"
                && drag.Normalized["to"] == null);
        }

        private static bool TestM22MutuallyExclusiveTargets()
        {
            var cases = new[]
            {
                ("manage_gameobject", "destroy", new JObject
                {
                    ["action"] = "destroy",
                    ["instance_id"] = 1,
                    ["path"] = "/Root",
                }),
                ("manage_components", "list", new JObject
                {
                    ["action"] = "list",
                    ["instance_id"] = 1,
                    ["path"] = "/Root",
                }),
                ("input", "inspect", new JObject
                {
                    ["action"] = "inspect",
                    ["instance_id"] = 1,
                    ["path"] = "/Canvas/Button",
                }),
            };
            if (cases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "ARGUMENT_CONFLICT"))
            {
                return Expect(nameof(TestM22MutuallyExclusiveTargets), false);
            }

            var screenshot = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject
                {
                    ["target"] = "/Root",
                    ["path"] = "/Root",
                });
            return Expect(nameof(TestM22MutuallyExclusiveTargets),
                screenshot.Error?.code == "ARGUMENT_CONFLICT");
        }

        private static bool TestM22ComplexSchemaValues()
        {
            var moveArray = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_gameobject"),
                new JObject
                {
                    ["action"] = "move",
                    ["instance_id"] = 1,
                    ["position"] = new JArray(1, 2, 3),
                },
                "move");
            var moveObject = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_gameobject"),
                new JObject
                {
                    ["action"] = "move",
                    ["instance_id"] = 1,
                    ["position"] = new JObject
                    {
                        ["x"] = 1,
                        ["y"] = 2,
                        ["z"] = 3,
                    },
                },
                "move");
            var invalidMove = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_gameobject"),
                new JObject
                {
                    ["action"] = "move",
                    ["instance_id"] = 1,
                    ["position"] = true,
                },
                "move");
            var componentValue = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_components"),
                new JObject
                {
                    ["action"] = "set",
                    ["component_id"] = 1,
                    ["property"] = "m_Test",
                    ["value"] = new JArray(1, 2, 3),
                },
                "set");
            var setParent = ToolContractRegistry.Get("manage_gameobject")
                .Actions["set_parent"];
            var parentSchema = setParent.InputSchema["properties"]?["parent"] as JObject;
            var validSequence = ToolContractValidator.Validate(
                ToolContractRegistry.Get("input"),
                new JObject
                {
                    ["action"] = "sequence",
                    ["steps"] = new JArray(new JObject
                    {
                        ["action"] = "keyboard",
                        ["key"] = "space",
                    }),
                },
                "sequence");
            var invalidNestedAction = ToolContractValidator.Validate(
                ToolContractRegistry.Get("input"),
                new JObject
                {
                    ["action"] = "sequence",
                    ["steps"] = new JArray(new JObject { ["action"] = "state" }),
                },
                "sequence");
            var invalidNestedField = ToolContractValidator.Validate(
                ToolContractRegistry.Get("input"),
                new JObject
                {
                    ["action"] = "sequence",
                    ["steps"] = new JArray(new JObject
                    {
                        ["action"] = "keyboard",
                        ["key"] = "space",
                        ["unknown"] = true,
                    }),
                },
                "sequence");
            return Expect(nameof(TestM22ComplexSchemaValues),
                moveArray.IsValid
                && moveObject.IsValid
                && invalidMove.Error?.code == "ARGUMENT_TYPE_MISMATCH"
                && componentValue.IsValid
                && validSequence.IsValid
                && invalidNestedAction.Error != null
                && invalidNestedField.Error != null
                && SchemaContainsType(parentSchema, "null"));
        }

        private static bool TestM22ScalarCompatibility()
        {
            var activeOn = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_gameobject"),
                new JObject
                {
                    ["action"] = "set_active",
                    ["instance_id"] = 1,
                    ["active"] = "on",
                },
                "set_active");
            var activeZero = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_gameobject"),
                new JObject
                {
                    ["action"] = "set_active",
                    ["instance_id"] = 1,
                    ["active"] = 0,
                },
                "set_active");
            var isolatedYes = ToolContractValidator.Validate(
                ToolContractRegistry.Get("screenshot"),
                new JObject
                {
                    ["isolated"] = "yes",
                    ["target"] = "/Root",
                });
            return Expect(nameof(TestM22ScalarCompatibility),
                activeOn.IsValid
                && activeOn.Normalized.Value<bool>("active")
                && activeZero.IsValid
                && !activeZero.Normalized.Value<bool>("active")
                && isolatedYes.IsValid
                && isolatedYes.Normalized.Value<bool>("isolated"));
        }

        private static bool TestM22OutputSchemas()
        {
            foreach (var toolName in new[]
            {
                "scene",
                "manage_gameobject",
                "manage_components",
                "manage_editor",
                "input",
            })
            {
                var contract = ToolContractRegistry.Get(toolName);
                if (contract.Actions.Values.Any(action => !HasOutputEnvelope(action.OutputSchema)))
                {
                    return Expect(nameof(TestM22OutputSchemas), false);
                }
            }

            var screenshot = ToolContractRegistry.Get("screenshot");
            var scene = ToolContractRegistry.Get("scene");
            var gameObject = ToolContractRegistry.Get("manage_gameobject");
            var components = ToolContractRegistry.Get("manage_components");
            return Expect(nameof(TestM22OutputSchemas),
                HasOutputEnvelope(screenshot.OutputSchema)
                && scene.Actions["load"].OutputSchema["properties"]?["data"]?["properties"]?["name"] != null
                && gameObject.Actions["get_transform"].OutputSchema["properties"]?["data"]?["properties"]?["transform"] != null
                && components.Actions["list"].OutputSchema["properties"]?["data"]?["properties"]?["components"] != null);
        }

        private static bool TestM23StrictToolCoverage()
        {
            var actionTools = new[]
            {
                "manage_assets",
                "manage_asset_import",
                "manage_material",
                "manage_prefab",
                "manage_animation",
                "manage_ui",
            };
            var defaultTools = new[] { "reserialize", "refresh_unity", "detect_assets" };
            return Expect(nameof(TestM23StrictToolCoverage),
                actionTools.All(tool =>
                {
                    var contract = ToolContractRegistry.Get(tool);
                    return contract?.Mode == ToolContractMode.Strict
                        && contract.Actions.Count > 0;
                })
                && defaultTools.All(tool =>
                {
                    var contract = ToolContractRegistry.Get(tool);
                    return contract?.Mode == ToolContractMode.Strict
                        && contract.InputSchema["additionalProperties"]?.Value<bool>() == false
                        && ToolContractValidator.Validate(contract, new JObject()).IsValid;
                }));
        }

        private static bool TestEveryM23ActionContract()
        {
            var count = 0;
            foreach (var entry in StrictM23Actions())
            {
                count++;
                var contract = ToolContractRegistry.Get(entry.tool);
                if (contract == null
                    || !contract.Actions.TryGetValue(entry.action, out var action)
                    || !action.IsStrict
                    || action.InputSchema["additionalProperties"]?.Value<bool>() != false
                    || action.InputSchema["properties"]?["action"]?["const"]?.Value<string>()
                        != entry.action
                    || !ToolContractValidator.Validate(contract, entry.input, entry.action).IsValid)
                {
                    return Expect(nameof(TestEveryM23ActionContract), false);
                }
            }

            return Expect(nameof(TestEveryM23ActionContract), count == 26);
        }

        private static bool TestM23ValidationFailures()
        {
            var missingCases = new[]
            {
                ("manage_assets", "mkdir", new JObject { ["action"] = "mkdir" }),
                ("manage_asset_import", "set", new JObject
                {
                    ["action"] = "set",
                    ["path"] = "Assets/Test.png",
                    ["property"] = "m_Test",
                }),
                ("manage_material", "create", new JObject
                {
                    ["action"] = "create",
                    ["path"] = "Assets/Test.mat",
                }),
                ("manage_prefab", "instantiate", new JObject { ["action"] = "instantiate" }),
                ("manage_animation", "set_curve", new JObject
                {
                    ["action"] = "set_curve",
                    ["path"] = "Assets/Test.anim",
                }),
                ("manage_ui", "create", new JObject { ["action"] = "create" }),
            };
            if (missingCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "MISSING_ARGUMENT"))
            {
                return Expect(nameof(TestM23ValidationFailures), false);
            }

            var wrongTypeCases = new[]
            {
                ("manage_assets", "find", new JObject
                {
                    ["action"] = "find",
                    ["limit"] = new JObject(),
                }),
                ("manage_asset_import", "get", new JObject
                {
                    ["action"] = "get",
                    ["path"] = new JObject(),
                }),
                ("manage_material", "get", new JObject
                {
                    ["action"] = "get",
                    ["path"] = new JObject(),
                }),
                ("manage_prefab", "instantiate", new JObject
                {
                    ["action"] = "instantiate",
                    ["path"] = new JObject(),
                }),
                ("manage_animation", "create_clip", new JObject
                {
                    ["action"] = "create_clip",
                    ["path"] = "Assets/Test.anim",
                    ["frame_rate"] = new JObject(),
                }),
                ("manage_ui", "create", new JObject
                {
                    ["action"] = "create",
                    ["element"] = new JObject(),
                }),
            };
            if (wrongTypeCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "ARGUMENT_TYPE_MISMATCH"))
            {
                return Expect(nameof(TestM23ValidationFailures), false);
            }

            foreach (var entry in StrictM23Actions())
            {
                var unknown = (JObject)entry.input.DeepClone();
                unknown["unknown_m23_property"] = true;
                if (ToolContractValidator.Validate(
                        ToolContractRegistry.Get(entry.tool),
                        unknown,
                        entry.action).Error?.code != "UNKNOWN_ARGUMENT")
                {
                    return Expect(nameof(TestM23ValidationFailures), false);
                }
            }

            foreach (var tool in new[] { "reserialize", "refresh_unity", "detect_assets" })
            {
                var contract = ToolContractRegistry.Get(tool);
                if (ToolContractValidator.Validate(
                        contract,
                        new JObject { ["unknown_m23_property"] = true }).Error?.code
                    != "UNKNOWN_ARGUMENT")
                {
                    return Expect(nameof(TestM23ValidationFailures), false);
                }
            }

            foreach (var tool in new[]
            {
                "manage_assets",
                "manage_asset_import",
                "manage_material",
                "manage_prefab",
                "manage_animation",
                "manage_ui",
            })
            {
                var unknownAction = CommandRouter.Dispatch(
                        tool,
                        new JObject { ["action"] = "not_real" })
                    .GetAwaiter()
                    .GetResult() as ErrorResponse;
                if (unknownAction?.code != "UNKNOWN_ACTION")
                    return Expect(nameof(TestM23ValidationFailures), false);
            }

            return Expect(nameof(TestM23ValidationFailures), true);
        }

        private static bool TestM23AliasesNormalize()
        {
            var reserializePositionals = ToolContractValidator.Validate(
                ToolContractRegistry.Get("reserialize"),
                new JObject
                {
                    ["args"] = new JArray(
                        "Assets/One.asset",
                        "Assets/Two.asset",
                        "Assets/Three.asset"),
                });
            var reserializePathAlias = ToolContractValidator.Validate(
                ToolContractRegistry.Get("reserialize"),
                new JObject { ["path"] = "Assets/One.asset" });
            return Expect(nameof(TestM23AliasesNormalize),
                reserializePositionals.IsValid
                && reserializePositionals.Normalized["paths"] is JArray paths
                && paths.Count == 3
                && reserializePathAlias.IsValid
                && reserializePathAlias.Normalized["paths"] is JArray aliasPaths
                && aliasPaths.Count == 1);
        }

        private static bool TestM23MutuallyExclusiveTargets()
        {
            var cases = new[]
            {
                ("manage_prefab", "create", new JObject
                {
                    ["action"] = "create",
                    ["path"] = "Assets/Test.prefab",
                    ["source"] = "/Root",
                    ["instance_id"] = 1,
                }),
                ("manage_ui", "get_rect", new JObject
                {
                    ["action"] = "get_rect",
                    ["path"] = "/Canvas",
                    ["instance_id"] = 1,
                }),
            };
            if (cases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "ARGUMENT_CONFLICT"))
            {
                return Expect(nameof(TestM23MutuallyExclusiveTargets), false);
            }

            var reserialize = ToolContractValidator.Validate(
                ToolContractRegistry.Get("reserialize"),
                new JObject
                {
                    ["path"] = "Assets",
                    ["paths"] = new JArray("Assets"),
                });
            return Expect(nameof(TestM23MutuallyExclusiveTargets),
                reserialize.Error?.code == "ARGUMENT_CONFLICT");
        }

        private static bool TestM23ComplexSchemaValues()
        {
            var assetProperties = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_assets"),
                new JObject
                {
                    ["action"] = "create",
                    ["path"] = "Assets/Test.asset",
                    ["type"] = "TestAsset",
                    ["properties"] = new JObject { ["enabled"] = true },
                },
                "create");
            var animationKeys = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_animation"),
                new JObject
                {
                    ["action"] = "set_curve",
                    ["path"] = "Assets/Test.anim",
                    ["type"] = "UnityEngine.Transform",
                    ["property"] = "m_LocalPosition.x",
                    ["keys"] = new JArray(new JObject
                    {
                        ["time"] = 0,
                        ["value"] = 1,
                    }),
                },
                "set_curve");
            var invalidKeys = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_animation"),
                new JObject
                {
                    ["action"] = "set_curve",
                    ["path"] = "Assets/Test.anim",
                    ["type"] = "UnityEngine.Transform",
                    ["property"] = "m_LocalPosition.x",
                    ["keys"] = new JArray(true),
                },
                "set_curve");
            var uiVector = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_ui"),
                new JObject
                {
                    ["action"] = "set_rect",
                    ["path"] = "/Canvas/Panel",
                    ["size_delta"] = new JArray(100, 50),
                },
                "set_rect");
            var uiScientificVector = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_ui"),
                new JObject
                {
                    ["action"] = "set_rect",
                    ["path"] = "/Canvas/Panel",
                    ["size_delta"] = "1e-3,+2",
                },
                "set_rect");
            var invalidUiVector = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_ui"),
                new JObject
                {
                    ["action"] = "set_rect",
                    ["path"] = "/Canvas/Panel",
                    ["size_delta"] = "not-a-vector",
                },
                "set_rect");
            return Expect(nameof(TestM23ComplexSchemaValues),
                assetProperties.IsValid
                && animationKeys.IsValid
                && invalidKeys.Error?.code == "ARGUMENT_TYPE_MISMATCH"
                && uiVector.IsValid
                && uiScientificVector.IsValid
                && !invalidUiVector.IsValid);
        }

        private static bool TestM23OutputSchemas()
        {
            foreach (var toolName in new[]
            {
                "manage_assets",
                "manage_asset_import",
                "manage_material",
                "manage_prefab",
                "manage_animation",
                "manage_ui",
            })
            {
                var contract = ToolContractRegistry.Get(toolName);
                if (contract.Actions.Values.Any(action => !HasOutputEnvelope(action.OutputSchema)))
                    return Expect(nameof(TestM23OutputSchemas), false);
            }

            var defaultTools = new[] { "reserialize", "refresh_unity", "detect_assets" };
            var assets = ToolContractRegistry.Get("manage_assets");
            var animation = ToolContractRegistry.Get("manage_animation");
            var ui = ToolContractRegistry.Get("manage_ui");
            var createAssetData = assets.Actions["create"].OutputSchema["properties"]?["data"];
            var assetSchemasValid =
                defaultTools.All(tool => HasOutputEnvelope(ToolContractRegistry.Get(tool).OutputSchema))
                && assets.Actions["find"].OutputSchema["properties"]?["data"]?["properties"]?["assets"] != null
                && createAssetData?["properties"]?["applied"]?["type"]?.Value<string>() == "array"
                && createAssetData?["properties"]?["applied"]?["items"]?["type"]?.Value<string>() == "string";
            var uiSchemasValid =
                animation.Actions["set_curve"].OutputSchema["properties"]?["data"]?["properties"]?["keys"] != null
                && ui.Actions["get_rect"].OutputSchema["properties"]?["data"]?["properties"]?["rect"]?["properties"]?["anchor_min"] != null;
            return Expect(nameof(TestM23OutputSchemas), assetSchemasValid && uiSchemasValid);
        }

        private static bool TestM24StrictToolCoverage()
        {
            var tools = new[]
            {
                "manage_packages",
                "run_tests",
                "profiler",
                "log",
                "exec",
                "menu",
            };
            return Expect(nameof(TestM24StrictToolCoverage),
                tools.All(tool => ToolContractRegistry.Get(tool)?.Mode
                    == ToolContractMode.Strict)
                && ToolContractRegistry.Get("manage_packages").Actions.Values
                    .All(action => action.IsStrict)
                && ToolContractRegistry.Get("profiler").Actions.Count == 6
                && ToolContractRegistry.Get("profiler").Actions.Values
                    .All(action => action.IsStrict));
        }

        private static bool TestEveryM24ActionContract()
        {
            var count = 0;
            foreach (var entry in StrictM24Actions())
            {
                count++;
                var contract = ToolContractRegistry.Get(entry.tool);
                if (contract == null
                    || !contract.Actions.TryGetValue(entry.action, out var action)
                    || !action.IsStrict
                    || action.InputSchema["additionalProperties"]?.Value<bool>() != false
                    || action.InputSchema["properties"]?["action"]?["const"]?.Value<string>()
                        != entry.action
                    || !ToolContractValidator.Validate(
                        contract,
                        entry.input,
                        entry.action).IsValid)
                {
                    return Expect(nameof(TestEveryM24ActionContract), false);
                }
            }

            return Expect(nameof(TestEveryM24ActionContract), count == 9);
        }

        private static bool TestM24ValidationFailures()
        {
            var missingCases = new[]
            {
                ("manage_packages", "add", new JObject { ["action"] = "add" }),
                ("manage_packages", "remove", new JObject { ["action"] = "remove" }),
                ("manage_packages", "embed", new JObject { ["action"] = "embed" }),
            };
            if (missingCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "MISSING_ARGUMENT"))
            {
                return Expect(nameof(TestM24ValidationFailures), false);
            }

            var defaultMissingCases = new[]
            {
                ("run_tests", new JObject()),
                ("log", new JObject()),
                ("exec", new JObject()),
                ("menu", new JObject()),
            };
            if (defaultMissingCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item2).Error?.code != "MISSING_ARGUMENT"))
            {
                return Expect(nameof(TestM24ValidationFailures), false);
            }

            var wrongTypeCases = new[]
            {
                ("manage_packages", "add", new JObject
                {
                    ["action"] = "add",
                    ["identifier"] = new JObject(),
                }),
                ("profiler", "hierarchy", new JObject
                {
                    ["action"] = "hierarchy",
                    ["frame"] = new JObject(),
                }),
            };
            if (wrongTypeCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item3,
                    entry.Item2).Error?.code != "ARGUMENT_TYPE_MISMATCH"))
            {
                return Expect(nameof(TestM24ValidationFailures), false);
            }

            var defaultWrongTypeCases = new[]
            {
                ("run_tests", new JObject
                {
                    ["mode"] = "EditMode",
                    ["async_results"] = new JObject(),
                }),
                ("log", new JObject { ["message"] = new JObject() }),
                ("exec", new JObject
                {
                    ["code"] = "return null;",
                    ["depth"] = new JObject(),
                }),
                ("menu", new JObject { ["menu_path"] = new JObject() }),
            };
            if (defaultWrongTypeCases.Any(entry =>
                ToolContractValidator.Validate(
                    ToolContractRegistry.Get(entry.Item1),
                    entry.Item2).Error?.code != "ARGUMENT_TYPE_MISMATCH"))
            {
                return Expect(nameof(TestM24ValidationFailures), false);
            }

            foreach (var entry in StrictM24Actions())
            {
                var unknown = (JObject)entry.input.DeepClone();
                unknown["unknown_m24_property"] = true;
                if (ToolContractValidator.Validate(
                        ToolContractRegistry.Get(entry.tool),
                        unknown,
                        entry.action).Error?.code != "UNKNOWN_ARGUMENT")
                {
                    return Expect(nameof(TestM24ValidationFailures), false);
                }
            }

            foreach (var entry in M24DefaultInputs())
            {
                var unknown = (JObject)entry.input.DeepClone();
                unknown["unknown_m24_property"] = true;
                if (ToolContractValidator.Validate(
                        ToolContractRegistry.Get(entry.tool),
                        unknown).Error?.code != "UNKNOWN_ARGUMENT")
                {
                    return Expect(nameof(TestM24ValidationFailures), false);
                }
            }

            foreach (var tool in new[] { "manage_packages", "profiler", "menu" })
            {
                var unknownAction = CommandRouter.Dispatch(
                        tool,
                        new JObject { ["action"] = "not_real" })
                    .GetAwaiter()
                    .GetResult() as ErrorResponse;
                if (unknownAction?.code != "UNKNOWN_ACTION")
                    return Expect(nameof(TestM24ValidationFailures), false);
            }

            return Expect(nameof(TestM24ValidationFailures), true);
        }

        private static bool TestM24AliasesNormalize()
        {
            var packagePositional = ToolContractValidator.Validate(
                ToolContractRegistry.Get("manage_packages"),
                new JObject
                {
                    ["action"] = "add",
                    ["args"] = new JArray("add", "com.example.package"),
                },
                "add");
            var execNoCache = ToolContractValidator.Validate(
                ToolContractRegistry.Get("exec"),
                new JObject
                {
                    ["code"] = "return null;",
                    ["nocache"] = true,
                });
            var execDashedNoCache = ToolContractValidator.Validate(
                ToolContractRegistry.Get("exec"),
                new JObject
                {
                    ["code"] = "return null;",
                    ["no-cache"] = true,
                });
            var execCommaUsings = ToolContractValidator.Validate(
                ToolContractRegistry.Get("exec"),
                new JObject
                {
                    ["code"] = "return null;",
                    ["usings"] = "UnityEditor,UnityEngine",
                });
            var runTestsPositional = ToolContractValidator.Validate(
                ToolContractRegistry.Get("run_tests"),
                new JObject { ["args"] = new JArray("editmode", "HeraAgent.Tests") });
            var menuPositional = ToolContractValidator.Validate(
                ToolContractRegistry.Get("menu"),
                new JObject { ["args"] = new JArray("Assets/Refresh") });
            var logLevelAliases = new[] { "warn", "err", "info" }
                .All(level => ToolContractValidator.Validate(
                    ToolContractRegistry.Get("log"),
                    new JObject
                    {
                        ["message"] = "marker",
                        ["level"] = level,
                    }).IsValid);

            return Expect(nameof(TestM24AliasesNormalize),
                packagePositional.IsValid
                && packagePositional.Normalized.Value<string>("identifier")
                    == "com.example.package"
                && execNoCache.IsValid
                && execNoCache.Normalized.Value<bool>("no_cache")
                && execDashedNoCache.IsValid
                && execDashedNoCache.Normalized.Value<bool>("no_cache")
                && execCommaUsings.IsValid
                && runTestsPositional.IsValid
                && runTestsPositional.Normalized.Value<string>("mode") == "EditMode"
                && runTestsPositional.Normalized.Value<string>("filter")
                    == "HeraAgent.Tests"
                && menuPositional.IsValid
                && menuPositional.Normalized.Value<string>("menu_path") == "Assets/Refresh"
                && logLevelAliases);
        }

        private static bool TestM8RestrictedExecContract()
        {
            var contract = ToolContractRegistry.Get("exec");
            var restricted = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["code"] = "return 1;",
                    ["security_mode"] = "restricted",
                });
            var dashed = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["code"] = "return 1;",
                    ["security-mode"] = "restricted",
                });
            var invalid = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["code"] = "return 1;",
                    ["security_mode"] = "unknown",
                });

            return Expect(nameof(TestM8RestrictedExecContract),
                restricted.IsValid
                && restricted.Normalized.Value<string>("security_mode") == "restricted"
                && dashed.IsValid
                && dashed.Normalized.Value<string>("security_mode") == "restricted"
                && invalid.Error?.code == "INVALID_ARGUMENT");
        }

        private static bool TestM24MutuallyExclusiveTargets()
        {
            var cases = new[]
            {
                new JObject
                {
                    ["action"] = "hierarchy",
                    ["frame"] = 1,
                    ["frames"] = 2,
                },
                new JObject
                {
                    ["action"] = "hierarchy",
                    ["frame"] = 1,
                    ["from"] = 0,
                },
                new JObject
                {
                    ["action"] = "hierarchy",
                    ["frames"] = 2,
                    ["to"] = 10,
                },
                new JObject
                {
                    ["action"] = "hierarchy",
                    ["root"] = "PlayerLoop",
                    ["parent"] = 1,
                },
            };
            return Expect(nameof(TestM24MutuallyExclusiveTargets),
                cases.All(input => ToolContractValidator.Validate(
                    ToolContractRegistry.Get("profiler"),
                    input,
                    "hierarchy").Error?.code == "ARGUMENT_CONFLICT")
                && ToolContractValidator.Validate(
                    ToolContractRegistry.Get("profiler"),
                    new JObject
                    {
                        ["frame"] = 1,
                        ["frames"] = 2,
                    }).Error?.code == "ARGUMENT_CONFLICT"
                && ToolContractValidator.Validate(
                    ToolContractRegistry.Get("profiler"),
                    new JObject
                    {
                        ["action"] = "hierarchy",
                        ["from"] = 0,
                        ["to"] = 10,
                    },
                    "hierarchy").IsValid);
        }

        private static bool TestM24OutputSchemas()
        {
            foreach (var tool in new[]
            {
                "manage_packages",
                "run_tests",
                "profiler",
                "log",
                "exec",
                "menu",
            })
            {
                if (!HasOutputEnvelope(ToolContractRegistry.Get(tool).OutputSchema))
                    return Expect(nameof(TestM24OutputSchemas), false);
            }

            foreach (var tool in new[] { "manage_packages", "profiler" })
            {
                if (ToolContractRegistry.Get(tool).Actions.Values
                    .Any(action => !HasOutputEnvelope(action.OutputSchema)))
                {
                    return Expect(nameof(TestM24OutputSchemas), false);
                }
            }

            var packages = ToolContractRegistry.Get("manage_packages");
            var profiler = ToolContractRegistry.Get("profiler");
            var runTests = ToolContractRegistry.Get("run_tests");
            var log = ToolContractRegistry.Get("log");
            var menu = ToolContractRegistry.Get("menu");
            var profilerDefaultData = profiler.OutputSchema["properties"]?["data"];
            var profilerHierarchyData = profiler.Actions["hierarchy"].OutputSchema[
                "properties"]?["data"];
            var profilerStatusData = profiler.Actions["status"].OutputSchema[
                "properties"]?["data"];
            var statusResponse = HeraAgent.Tools.ManageProfiler.HandleCommand(
                new JObject { ["action"] = "status" }) as SuccessResponse;
            var runtimeStatusProperties = statusResponse?.data == null
                ? Array.Empty<string>()
                : JObject.FromObject(statusResponse.data).Properties()
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
            var schemaStatusProperties = (profilerStatusData?["properties"] as JObject)?
                .Properties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            var packageSchemasValid =
                packages.Actions["list"].OutputSchema["properties"]?["data"]?
                    ["properties"]?["packages"]?["items"]?["properties"]?["name"] != null
                && packages.Actions["add"].OutputSchema["properties"]?["data"]?
                    ["properties"]?["job_id"] != null;
            var profilerSchemasValid =
                profilerDefaultData?["properties"]?["threadIndex"] != null
                && profilerDefaultData?["properties"]?["thread_index"] == null
                && profilerHierarchyData?["properties"]?["parentName"] != null
                && profilerHierarchyData?["properties"]?["parent_name"] == null
                && profilerHierarchyData?["properties"]?["children"]?
                    ["type"]?.Value<string>() == "array"
                && profilerStatusData?["properties"]?["firstFrame"] != null
                && profilerStatusData?["properties"]?["first_frame"] == null
                && runtimeStatusProperties.SequenceEqual(schemaStatusProperties);
            var remainingSchemasValid =
                runTests.OutputSchema["properties"]?["data"]?
                    ["properties"]?["run_id"] != null
                && runTests.OutputSchema["properties"]?["data"]?
                    ["properties"]?["failures"]?["items"]?["type"]?.Value<string>()
                        == "string"
                && log.OutputSchema["properties"]?["data"]?
                    ["properties"]?["level"] != null
                && menu.Actions["list"].OutputSchema["properties"]?["data"]?
                    ["properties"]?["groups"]?["type"]?.Value<string>() == "array";
            return Expect(nameof(TestM24OutputSchemas),
                packageSchemasValid && profilerSchemasValid && remainingSchemasValid);
        }

        private static bool HasOutputEnvelope(JObject schema)
        {
            var properties = schema?["properties"] as JObject;
            return properties?["success"] != null
                && properties["message"] != null
                && properties["data"] != null;
        }

        private static bool SchemaContainsType(JObject schema, string expected)
        {
            if (schema == null)
                return false;
            var type = schema["type"];
            if (type?.Type == JTokenType.String && type.Value<string>() == expected)
                return true;
            if (type is JArray types && types.Values<string>().Contains(expected))
                return true;
            foreach (var keyword in new[] { "oneOf", "anyOf", "allOf" })
            {
                if (schema[keyword] is JArray branches
                    && branches.OfType<JObject>().Any(branch =>
                        SchemaContainsType(branch, expected)))
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<(string tool, string action, JObject input)> StrictM22Actions()
        {
            yield return ("scene", "load", new JObject
            {
                ["action"] = "load",
                ["path"] = "Assets/Test.unity",
            });
            yield return ("scene", "save", new JObject { ["action"] = "save" });
            yield return ("scene", "close", new JObject
            {
                ["action"] = "close",
                ["path"] = "Assets/Test.unity",
            });
            yield return ("scene", "hierarchy", new JObject
            {
                ["action"] = "hierarchy",
                ["depth"] = 2,
                ["max_nodes"] = 50,
            });

            yield return ("manage_gameobject", "create", new JObject { ["action"] = "create" });
            foreach (var action in new[] { "destroy", "duplicate", "set_parent", "get_transform" })
            {
                yield return ("manage_gameobject", action, new JObject
                {
                    ["action"] = action,
                    ["instance_id"] = 1,
                });
            }
            yield return ("manage_gameobject", "move", new JObject
            {
                ["action"] = "move",
                ["instance_id"] = 1,
                ["position"] = "0,0,0",
            });
            yield return ("manage_gameobject", "set_active", new JObject
            {
                ["action"] = "set_active",
                ["instance_id"] = 1,
                ["active"] = true,
            });
            yield return ("manage_gameobject", "set_name", new JObject
            {
                ["action"] = "set_name",
                ["instance_id"] = 1,
                ["name"] = "Renamed",
            });

            yield return ("manage_components", "add", new JObject
            {
                ["action"] = "add",
                ["instance_id"] = 1,
                ["type"] = "Rigidbody",
            });
            yield return ("manage_components", "list", new JObject
            {
                ["action"] = "list",
                ["instance_id"] = 1,
            });
            foreach (var action in new[] { "remove", "get" })
            {
                yield return ("manage_components", action, new JObject
                {
                    ["action"] = action,
                    ["component_id"] = 1,
                });
            }
            yield return ("manage_components", "set", new JObject
            {
                ["action"] = "set",
                ["component_id"] = 1,
                ["property"] = "m_Test",
                ["value"] = true,
            });

            foreach (var action in new[] { "play", "stop", "pause" })
                yield return ("manage_editor", action, new JObject { ["action"] = action });
            yield return ("manage_editor", "set_active_tool", new JObject
            {
                ["action"] = "set_active_tool",
                ["tool_name"] = "Move",
            });
            foreach (var action in new[] { "add_tag", "remove_tag" })
            {
                yield return ("manage_editor", action, new JObject
                {
                    ["action"] = action,
                    ["tag_name"] = "HeraTest",
                });
            }
            foreach (var action in new[] { "add_layer", "remove_layer" })
            {
                yield return ("manage_editor", action, new JObject
                {
                    ["action"] = action,
                    ["layer_name"] = "HeraTest",
                });
            }
            yield return ("manage_editor", "get_selection", new JObject { ["action"] = "get_selection" });
            yield return ("manage_editor", "get_selection", new JObject
            {
                ["action"] = "get_selection",
                ["durable"] = true,
            });
            yield return ("manage_editor", "set_selection", new JObject
            {
                ["action"] = "set_selection",
                ["targets"] = new JArray("/HeraTest"),
            });

            yield return ("input", "state", new JObject { ["action"] = "state" });
            yield return ("input", "keyboard", new JObject
            {
                ["action"] = "keyboard",
                ["key"] = "space",
            });
            yield return ("input", "mouse", new JObject
            {
                ["action"] = "mouse",
                ["mode"] = "move",
                ["position"] = "100,200",
            });
            yield return ("input", "sequence", new JObject
            {
                ["action"] = "sequence",
                ["steps"] = new JArray(new JObject
                {
                    ["action"] = "keyboard",
                    ["key"] = "space",
                }),
            });
            yield return ("input", "record", new JObject
            {
                ["action"] = "record",
                ["mode"] = "status",
            });
            yield return ("input", "replay", new JObject
            {
                ["action"] = "replay",
                ["path"] = "Library/HeraAgent/Recordings/input.json",
            });
            foreach (var action in new[]
            {
                "inspect",
                "click",
                "pointer_down",
                "pointer_up",
                "submit",
                "scroll",
                "drag",
            })
            {
                yield return ("input", action, new JObject
                {
                    ["action"] = action,
                    ["path"] = "/Canvas/Target",
                });
            }
        }

        private static IEnumerable<(string tool, string action, JObject input)> StrictM23Actions()
        {
            yield return ("manage_assets", "find", new JObject
            {
                ["action"] = "find",
                ["filter"] = "Test",
            });
            foreach (var action in new[] { "mkdir", "delete" })
            {
                yield return ("manage_assets", action, new JObject
                {
                    ["action"] = action,
                    ["path"] = "Assets/Test",
                });
            }
            yield return ("manage_assets", "create", new JObject
            {
                ["action"] = "create",
                ["path"] = "Assets/Test.asset",
                ["type"] = "TestAsset",
            });
            foreach (var action in new[] { "copy", "move" })
            {
                yield return ("manage_assets", action, new JObject
                {
                    ["action"] = action,
                    ["path"] = "Assets/Source.asset",
                    ["new_path"] = "Assets/Destination.asset",
                });
            }

            yield return ("manage_asset_import", "get", new JObject
            {
                ["action"] = "get",
                ["path"] = "Assets/Test.png",
            });
            yield return ("manage_asset_import", "set", new JObject
            {
                ["action"] = "set",
                ["path"] = "Assets/Test.png",
                ["property"] = "m_Test",
                ["value"] = true,
            });

            yield return ("manage_material", "create", new JObject
            {
                ["action"] = "create",
                ["path"] = "Assets/Test.mat",
                ["shader"] = "Standard",
            });
            yield return ("manage_material", "get", new JObject
            {
                ["action"] = "get",
                ["path"] = "Assets/Test.mat",
            });
            yield return ("manage_material", "set", new JObject
            {
                ["action"] = "set",
                ["path"] = "Assets/Test.mat",
                ["property"] = "_Color",
                ["value"] = "#FFFFFFFF",
            });
            yield return ("manage_material", "set_shader", new JObject
            {
                ["action"] = "set_shader",
                ["path"] = "Assets/Test.mat",
                ["shader"] = "Standard",
            });

            yield return ("manage_prefab", "create", new JObject
            {
                ["action"] = "create",
                ["path"] = "Assets/Test.prefab",
                ["source"] = "/Root",
            });
            yield return ("manage_prefab", "instantiate", new JObject
            {
                ["action"] = "instantiate",
                ["path"] = "Assets/Test.prefab",
            });
            foreach (var action in new[] { "add_component", "remove_component" })
            {
                yield return ("manage_prefab", action, new JObject
                {
                    ["action"] = action,
                    ["path"] = "Assets/Test.prefab",
                    ["component"] = "Rigidbody",
                });
            }

            yield return ("manage_animation", "create_clip", new JObject
            {
                ["action"] = "create_clip",
                ["path"] = "Assets/Test.anim",
            });
            yield return ("manage_animation", "set_curve", new JObject
            {
                ["action"] = "set_curve",
                ["path"] = "Assets/Test.anim",
                ["type"] = "UnityEngine.Transform",
                ["property"] = "m_LocalPosition.x",
                ["keys"] = new JArray(new JObject { ["time"] = 0, ["value"] = 0 }),
            });
            yield return ("manage_animation", "create_controller", new JObject
            {
                ["action"] = "create_controller",
                ["path"] = "Assets/Test.controller",
            });
            yield return ("manage_animation", "add_parameter", new JObject
            {
                ["action"] = "add_parameter",
                ["path"] = "Assets/Test.controller",
                ["name"] = "Speed",
                ["type"] = "float",
            });
            yield return ("manage_animation", "add_state", new JObject
            {
                ["action"] = "add_state",
                ["path"] = "Assets/Test.controller",
                ["name"] = "Idle",
            });
            yield return ("manage_animation", "add_transition", new JObject
            {
                ["action"] = "add_transition",
                ["path"] = "Assets/Test.controller",
                ["from"] = "Idle",
                ["to"] = "Run",
            });
            yield return ("manage_animation", "get_clip", new JObject
            {
                ["action"] = "get_clip",
                ["path"] = "Assets/Test.anim",
                ["include_keys"] = true,
            });
            yield return ("manage_animation", "get_controller", new JObject
            {
                ["action"] = "get_controller",
                ["path"] = "Assets/Test.controller",
            });

            yield return ("manage_ui", "create", new JObject
            {
                ["action"] = "create",
                ["element"] = "panel",
            });
            yield return ("manage_ui", "get_rect", new JObject
            {
                ["action"] = "get_rect",
                ["path"] = "/Canvas",
            });
            yield return ("manage_ui", "set_anchor", new JObject
            {
                ["action"] = "set_anchor",
                ["path"] = "/Canvas/Panel",
                ["preset"] = "stretch",
            });
            yield return ("manage_ui", "set_rect", new JObject
            {
                ["action"] = "set_rect",
                ["path"] = "/Canvas/Panel",
                ["size_delta"] = "100,50",
            });

        }

        private static IEnumerable<(string tool, string action, JObject input)> StrictM24Actions()
        {
            yield return ("manage_packages", "list", new JObject { ["action"] = "list" });
            foreach (var action in new[] { "add", "remove", "embed" })
            {
                yield return ("manage_packages", action, new JObject
                {
                    ["action"] = action,
                    ["identifier"] = "com.example.package",
                });
            }

            yield return ("profiler", "hierarchy", new JObject { ["action"] = "hierarchy" });
            foreach (var action in new[] { "enable", "disable", "status", "clear" })
                yield return ("profiler", action, new JObject { ["action"] = action });
        }

        private static IEnumerable<(string tool, JObject input)> M24DefaultInputs()
        {
            yield return ("run_tests", new JObject { ["mode"] = "EditMode" });
            yield return ("profiler", new JObject());
            yield return ("log", new JObject { ["message"] = "marker" });
            yield return ("exec", new JObject { ["code"] = "return null;" });
            yield return ("menu", new JObject { ["menu_path"] = "Assets/Refresh" });
        }

        private static IEnumerable<(string tool, string action)> StrictActions()
        {
            yield return ("scene", "info");
            yield return ("scene", "list");
            yield return ("menu", "list");
        }

        private static bool Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }

        [HeraTool(
            Name = "m21_deprecated_alias_fixture",
            ContractMode = ToolContractMode.Strict,
            Enabled = false)]
        private sealed class DeprecatedAliasFixture
        {
            public sealed class Parameters
            {
                [ToolParameter(
                    "Fixture value.",
                    Aliases = new[] { "old_value" },
                    Deprecated = true)]
                public string Value { get; set; }

                [ToolParameter(
                    "Fixture enum.",
                    SchemaJson = "{\"type\":\"string\",\"enum\":[\"first\",\"second\"]}")]
                public string Mode { get; set; }

                [ToolParameter("Fixture URI.", Format = "uri")]
                public string Uri { get; set; }
            }
        }

        [HeraTool(Name = "m21_invalid_schema_fixture", Enabled = false)]
        private sealed class InvalidSchemaFixture
        {
            public sealed class Parameters
            {
                [ToolParameter("Invalid schema fixture.", SchemaJson = "{")]
                public string Value { get; set; }
            }
        }

        [HeraTool(Name = "m21_invalid_keyword_schema_fixture", Enabled = false)]
        private sealed class InvalidKeywordSchemaFixture
        {
            public sealed class Parameters
            {
                [ToolParameter(
                    "Invalid schema keyword fixture.",
                    SchemaJson = "{\"type\":\"nonsense\"}")]
                public string Value { get; set; }
            }
        }

        [HeraTool(
            Name = "m21_nullable_fixture",
            ContractMode = ToolContractMode.Strict,
            Enabled = false)]
        private sealed class NullableFixture
        {
            public sealed class Parameters
            {
                [ToolParameter("Nullable fixture.", AllowNull = true)]
                public string Value { get; set; }
            }
        }

        [HeraTool(Name = "m21_class_action_fixture", Enabled = false)]
        [HeraActionContract("inspect", typeof(ClassActionFixture.InspectParameters))]
        private sealed class ClassActionFixture
        {
            public sealed class InspectParameters
            {
            }

            public static object HandleCommand(JObject input)
            {
                return new SuccessResponse("OK");
            }
        }
    }
}
