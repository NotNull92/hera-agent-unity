# Unity Pipeline parity audit ledger

Checkout: `c69a09204794a7a41ef2b774e1ab95913ee1ae4b` plus the approved parity worktree changes.

Scope follows `review-hera-agent-unity`: all 351 Go/C# source files (66,068 lines), relevant metadata and package files, installers/workflows, generated-guide producer, help, and public contract documents. Generated mirrors are validated through their producer and byte-for-byte drift test rather than treated as independent implementations.

Every source row received a source/AST inventory pass, definition/reference trace,
and its applicable test or generator gate. Changed code and the transport,
contract, safety, and Editor lifecycle paths were also read directly end to end.
The completion gate rejects `pending`, `unread`, or `untraced` anywhere below.

## Source coverage

| Path | Lane | Source/AST audit | Callers/consumers | Tests/docs | Open question |
|---|---|---|---|---|---|
| `AgentConnector/Editor/AssemblyInfo.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/Attributes/HeraActionAttribute.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/Attributes/HeraToolAttribute.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/CommandRouter.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ApprovalAuthority.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ApprovalPolicy.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/AssetConfigFile.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/AssetDetector.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/AssetPathGuard.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/AssetRefresh.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/AssetReserializer.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/AtomicFile.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/BundleStore.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/CommandRequestContext.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ComponentTypeResolver.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/EditorUpdate.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/EntityIdCompat.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/GameFeelStore.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/GameObjectComponents.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/HeraSettings.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/HierarchyPath.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaEventSystem.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaEventSystemActions.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaInputSystem.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaRecording.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaReplay.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaResolver.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaSequence.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaSequencePlan.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/InputQaTypes.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/Levenshtein.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ObjectIdentity.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/OperationLedger.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/OperationLedgerRecord.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/OutputFilePolicy.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/PackageJobState.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ParamCoercion.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ProjectIdentity.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ProtocolContracts.Generated.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/Response.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/SchemaUtility.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/SerializedPropertyValue.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/StringCaseUtility.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/TargetResolver.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolActionContractBuilder.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolCatalogBuilder.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractCanonicalJson.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractModels.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractProfiles.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractRegistry.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractSafety.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractSafetyRules.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractSchemaBuilder.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolContractValidator.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolMetadata.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/ToolParams.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/UIJuiceGuide.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/UiEventSystem.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/UiSlopStore.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/UnityDocsStore.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/UnityPitfalls.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Core/UnityVersionCompat.cs` | C# Core | complete | traced | gated | none |
| `AgentConnector/Editor/Heartbeat.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/HeraAgentAssetConfigWindow.Model.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/HeraAgentAssetConfigWindow.View.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/HeraAgentAssetConfigWindow.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/HttpServer.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/TestRunner/RunTests.cs` | test runner | complete | traced | gated | none |
| `AgentConnector/Editor/TestRunner/TestRunnerState.cs` | test runner | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ApprovalPolicyTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/AssetConfigPersistenceTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/AssetMutationPreflightTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/AtomicFileTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/EntityIdCompatTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ExecCompileCacheTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ExecRestrictedTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ExecSerializerTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/GameFeelStoreTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/HierarchyPathTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/InputQaRecordingTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/InputQaSequenceTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/InputQaTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/OperationLedgerTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/OutputFilePolicyTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ProjectIdentityTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ReadConsoleTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ReleaseGateTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ScreenshotAnnotationTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ScreenshotPhysicsTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolCatalogTestSupport.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolCatalogTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolContractTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolDiscoveryTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolProfileTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolSafetyExpectations.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/ToolSafetyTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/UiSlopStoreTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/UnityDocsStoreTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/UnityVersionCompatTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/ToolDiscovery.cs` | Editor lifecycle | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/Bake.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/Build.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/DescribeShader.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/DescribeType.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/DetectAssets.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/EditorScreenshot.Isolated.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/EditorScreenshot.PhysicsAnnotations.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/EditorScreenshot.UiAnnotations.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/EditorScreenshot.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecCompileCache.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.AssemblyLoader.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.Compilation.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.Restricted.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.Serializer.UnityObjects.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.Serializer.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.SourceBuilder.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteCsharp.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ExecuteMenuItem.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/FindGameObjects.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/FindMethod.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/GameFeel.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/Input.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/InputRecordingContract.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/InputSequenceContract.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ListAssemblies.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/LogToConsole.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageAnimation.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageAssetImport.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageAssets.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageComponents.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageEditor.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageGameObject.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageMaterial.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManagePackages.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManagePrefab.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageProfiler.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageScene.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageSettings.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageUI.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ReadConsole.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/RefreshUnity.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ReserializeAssets.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/UiSlop.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/UnityDocs.cs` | HeraTool | complete | traced | gated | none |
| `cmd/asset_config.go` | Go CLI | complete | traced | gated | none |
| `cmd/asset_config_dispatch_test.go` | test | complete | traced | gated | none |
| `cmd/batch.go` | Go CLI | complete | traced | gated | none |
| `cmd/batch_test.go` | test | complete | traced | gated | none |
| `cmd/batch_test_helpers_test.go` | test | complete | traced | gated | none |
| `cmd/build.go` | Go CLI | complete | traced | gated | none |
| `cmd/call.go` | Go CLI | complete | traced | gated | none |
| `cmd/call_action_schema_test.go` | test | complete | traced | gated | none |
| `cmd/call_approval.go` | Go CLI | complete | traced | gated | none |
| `cmd/call_input.go` | Go CLI | complete | traced | gated | none |
| `cmd/call_safety.go` | Go CLI | complete | traced | gated | none |
| `cmd/call_safety_test.go` | test | complete | traced | gated | none |
| `cmd/call_test.go` | test | complete | traced | gated | none |
| `cmd/call_tty.go` | Go CLI | complete | traced | gated | none |
| `cmd/config.go` | Go CLI | complete | traced | gated | none |
| `cmd/config_test.go` | test | complete | traced | gated | none |
| `cmd/deferred_delete_unix.go` | Go CLI | complete | traced | gated | none |
| `cmd/deferred_delete_windows.go` | Go CLI | complete | traced | gated | none |
| `cmd/discovery.go` | Go CLI | complete | traced | gated | none |
| `cmd/dispatch.go` | Go CLI | complete | traced | gated | none |
| `cmd/doctor.go` | Go CLI | complete | traced | gated | none |
| `cmd/doctor_agent_rules.go` | Go CLI | complete | traced | gated | none |
| `cmd/doctor_test.go` | test | complete | traced | gated | none |
| `cmd/editor.go` | Go CLI | complete | traced | gated | none |
| `cmd/editor_bootstrap.go` | Go CLI | complete | traced | gated | none |
| `cmd/editor_bootstrap_test.go` | test | complete | traced | gated | none |
| `cmd/editor_install.go` | Go CLI | complete | traced | gated | none |
| `cmd/editor_process.go` | Go CLI | complete | traced | gated | none |
| `cmd/editor_process_unix.go` | Go CLI | complete | traced | gated | none |
| `cmd/editor_process_windows.go` | Go CLI | complete | traced | gated | none |
| `cmd/editor_process_windows_test.go` | test | complete | traced | gated | none |
| `cmd/editor_test.go` | test | complete | traced | gated | none |
| `cmd/exec_cache_integration_test.go` | test | complete | traced | gated | none |
| `cmd/help.go` | Go CLI | complete | traced | gated | none |
| `cmd/help_test.go` | test | complete | traced | gated | none |
| `cmd/install.go` | Go CLI | complete | traced | gated | none |
| `cmd/install_test.go` | test | complete | traced | gated | none |
| `cmd/legacy_approval.go` | Go CLI | complete | traced | gated | none |
| `cmd/legacy_approval_test.go` | test | complete | traced | gated | none |
| `cmd/legacy_compat_test.go` | test | complete | traced | gated | none |
| `cmd/legacy_tool.go` | Go CLI | complete | traced | gated | none |
| `cmd/legacy_tool_test.go` | test | complete | traced | gated | none |
| `cmd/manage_packages.go` | Go CLI | complete | traced | gated | none |
| `cmd/mcp.go` | Go CLI | complete | traced | gated | none |
| `cmd/mcp_resources_test.go` | test | complete | traced | gated | none |
| `cmd/mcp_test.go` | test | complete | traced | gated | none |
| `cmd/mcp_test_helpers_test.go` | test | complete | traced | gated | none |
| `cmd/path_check.go` | Go CLI | complete | traced | gated | none |
| `cmd/paths.go` | Go CLI | complete | traced | gated | none |
| `cmd/paths_windows.go` | Go CLI | complete | traced | gated | none |
| `cmd/root.go` | Go CLI | complete | traced | gated | none |
| `cmd/root_test.go` | test | complete | traced | gated | none |
| `cmd/scene_integration_test.go` | test | complete | traced | gated | none |
| `cmd/send.go` | Go CLI | complete | traced | gated | none |
| `cmd/status.go` | Go CLI | complete | traced | gated | none |
| `cmd/status_test.go` | test | complete | traced | gated | none |
| `cmd/stdin_test.go` | test | complete | traced | gated | none |
| `cmd/task.go` | Go CLI | complete | traced | gated | none |
| `cmd/task_test.go` | test | complete | traced | gated | none |
| `cmd/test.go` | Go CLI | complete | traced | gated | none |
| `cmd/test_test.go` | test | complete | traced | gated | none |
| `cmd/uninstall.go` | Go CLI | complete | traced | gated | none |
| `cmd/uninstall_unix.go` | Go CLI | complete | traced | gated | none |
| `cmd/uninstall_windows.go` | Go CLI | complete | traced | gated | none |
| `cmd/unity_docs.go` | Go CLI | complete | traced | gated | none |
| `cmd/update.go` | Go CLI | complete | traced | gated | none |
| `cmd/update_test.go` | test | complete | traced | gated | none |
| `cmd/version_check.go` | Go CLI | complete | traced | gated | none |
| `cmd/version_check_test.go` | test | complete | traced | gated | none |
| `internal/assetconfig/categories.go` | Go core | complete | traced | gated | none |
| `internal/assetconfig/config.go` | Go core | complete | traced | gated | none |
| `internal/assetconfig/config_test.go` | test | complete | traced | gated | none |
| `internal/assetconfig/json.go` | Go core | complete | traced | gated | none |
| `internal/assetconfig/lock_test.go` | test | complete | traced | gated | none |
| `internal/assetconfig/persistence.go` | Go core | complete | traced | gated | none |
| `internal/assetconfig/process_unix.go` | Go core | complete | traced | gated | none |
| `internal/assetconfig/process_unix_test.go` | test | complete | traced | gated | none |
| `internal/assetconfig/process_windows.go` | Go core | complete | traced | gated | none |
| `internal/assetconfig/process_windows_test.go` | test | complete | traced | gated | none |
| `internal/client/approval.go` | transport/state | complete | traced | gated | none |
| `internal/client/cache.go` | transport/state | complete | traced | gated | none |
| `internal/client/client.go` | transport/state | complete | traced | gated | none |
| `internal/client/client_integration_test.go` | test | complete | traced | gated | none |
| `internal/client/client_test.go` | test | complete | traced | gated | none |
| `internal/client/heartbeat_scan_test.go` | test | complete | traced | gated | none |
| `internal/client/operation.go` | transport/state | complete | traced | gated | none |
| `internal/client/operation_retry_test.go` | test | complete | traced | gated | none |
| `internal/client/probe.go` | transport/state | complete | traced | gated | none |
| `internal/client/process_unix.go` | transport/state | complete | traced | gated | none |
| `internal/client/process_windows.go` | transport/state | complete | traced | gated | none |
| `internal/client/reload_retry.go` | transport/state | complete | traced | gated | none |
| `internal/client/reload_retry_test.go` | test | complete | traced | gated | none |
| `internal/client/target_identity.go` | transport/state | complete | traced | gated | none |
| `internal/client/transport.go` | transport/state | complete | traced | gated | none |
| `internal/client/transport_test.go` | test | complete | traced | gated | none |
| `internal/client/types.go` | transport/state | complete | traced | gated | none |
| `internal/client/types_test.go` | test | complete | traced | gated | none |
| `internal/logutil/suppress.go` | Go core | complete | traced | gated | none |
| `internal/mcpserver/approval_middleware.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/catalog_refresh.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/compact_search.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/compact_tools.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/compact_tools_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/config.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/discovery.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/m10_test_helpers_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/m11_approval_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/m13_invalidation_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/m13_test_helpers_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/middleware.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/native_tools.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/native_tools_profiles_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/native_tools_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/native_tools_test_helpers_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/profiles.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/profiles_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/result_resources.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/result_resources_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/results.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/runtime.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/server.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/server_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/tasks.go` | MCP/tasks | complete | traced | gated | none |
| `internal/mcpserver/tasks_test.go` | test | complete | traced | gated | none |
| `internal/mcpserver/tool_contract.go` | MCP/tasks | complete | traced | gated | none |
| `internal/paths/paths.go` | Go core | complete | traced | gated | none |
| `internal/policy/approval.go` | Go core | complete | traced | gated | none |
| `internal/policy/resolver.go` | Go core | complete | traced | gated | none |
| `internal/policy/types.go` | Go core | complete | traced | gated | none |
| `internal/policy/types_test.go` | test | complete | traced | gated | none |
| `internal/poll/backoff.go` | transport/state | complete | traced | gated | none |
| `internal/poll/poll.go` | transport/state | complete | traced | gated | none |
| `internal/poll/poll_test.go` | test | complete | traced | gated | none |
| `internal/protocol/contracts_gen.go` | Go core | complete | traced | gated | none |
| `internal/resultstore/prune.go` | Go core | complete | traced | gated | none |
| `internal/resultstore/store.go` | Go core | complete | traced | gated | none |
| `internal/resultstore/store_test.go` | test | complete | traced | gated | none |
| `internal/schema/compiler.go` | Go core | complete | traced | gated | none |
| `internal/schema/compiler_test.go` | test | complete | traced | gated | none |
| `internal/schema/errors.go` | Go core | complete | traced | gated | none |
| `internal/taskbridge/list.go` | MCP/tasks | complete | traced | gated | none |
| `internal/taskbridge/list_test.go` | test | complete | traced | gated | none |
| `internal/taskbridge/taskbridge.go` | MCP/tasks | complete | traced | gated | none |
| `internal/taskbridge/taskbridge_test.go` | test | complete | traced | gated | none |
| `internal/telemetry/event.go` | Go core | complete | traced | gated | none |
| `internal/telemetry/event_test.go` | test | complete | traced | gated | none |
| `internal/telemetry/jsonl.go` | Go core | complete | traced | gated | none |
| `internal/telemetry/jsonl_test.go` | test | complete | traced | gated | none |
| `internal/telemetry/summary.go` | Go core | complete | traced | gated | none |
| `internal/telemetry/summary_test.go` | test | complete | traced | gated | none |
| `internal/toolregistry/cache.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/cache_files.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/canonical.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/catalog.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/catalog_test.go` | test | complete | traced | gated | none |
| `internal/toolregistry/errors.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/identity.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/legacy_provider.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/live_integration_test.go` | test | complete | traced | gated | none |
| `internal/toolregistry/profiles.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/profiles_validation_test.go` | test | complete | traced | gated | none |
| `internal/toolregistry/provider.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/provider_test.go` | test | complete | traced | gated | none |
| `internal/toolregistry/registry.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/registry_test.go` | test | complete | traced | gated | none |
| `internal/toolregistry/types.go` | Go core | complete | traced | gated | none |
| `internal/toolregistry/unity_provider.go` | Go core | complete | traced | gated | none |
| `internal/tui/assetconfig.go` | Go core | complete | traced | gated | none |
| `internal/tui/detect.go` | Go core | complete | traced | gated | none |
| `internal/tui/style.go` | Go core | complete | traced | gated | none |
| `internal/unitystate/state.go` | transport/state | complete | traced | gated | none |
| `main.go` | Editor lifecycle | complete | traced | gated | none |
| `tools/benchmark-mcp/fixture.go` | generator/gate | complete | traced | gated | none |
| `tools/benchmark-mcp/fixture_test.go` | test | complete | traced | gated | none |
| `tools/benchmark-mcp/main.go` | generator/gate | complete | traced | gated | none |
| `tools/benchmark-mcp/observation.go` | generator/gate | complete | traced | gated | none |
| `tools/benchmark-mcp/run.go` | generator/gate | complete | traced | gated | none |
| `tools/benchmark-mcp/run_test.go` | test | complete | traced | gated | none |
| `tools/build-game-feel-docs/main.go` | generator/gate | complete | traced | gated | none |
| `tools/build-ui-slop-docs/main.go` | generator/gate | complete | traced | gated | none |
| `tools/build-ui-slop-docs/main_test.go` | test | complete | traced | gated | none |
| `tools/build-unity-docs/main.go` | generator/gate | complete | traced | gated | none |
| `tools/build-unity-docs/main_test.go` | test | complete | traced | gated | none |
| `tools/build-unity-docs/parser.go` | generator/gate | complete | traced | gated | none |
| `tools/catalog-payload-report/main.go` | generator/gate | complete | traced | gated | none |
| `tools/catalog-payload-report/main_test.go` | test | complete | traced | gated | none |
| `tools/generate-runtime-contracts/main.go` | generator/gate | complete | traced | gated | none |
| `tools/generate-runtime-contracts/main_test.go` | test | complete | traced | gated | none |
| `tools/sync-agent-guides/main.go` | generator/gate | complete | traced | gated | none |
| `tools/sync-agent-guides/main_test.go` | test | complete | traced | gated | none |
| `tools/validate-connector-package/main.go` | generator/gate | complete | traced | gated | none |
| `tools/validate-connector-package/main_test.go` | test | complete | traced | gated | none |
| `tools/validate-tool-catalog/main.go` | generator/gate | complete | traced | gated | none |
| `tools/validate-tool-catalog/main_test.go` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/AnimationTimelineToolTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/BuildToolTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/EditorUiCaptureTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/PipelineSettingsToolTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tests/SceneGameObjectToolTests.cs` | test | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/Build.Options.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/EditorScreenshot.EditorUi.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageAnimation.Extended.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageEditor.Focus.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageGameObject.Properties.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageScene.Authoring.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageSettings.Pipeline.cs` | HeraTool | complete | traced | gated | none |
| `AgentConnector/Editor/Tools/ManageTimeline.cs` | HeraTool | complete | traced | gated | none |

## Non-source lanes

| Lane | Inventory | Read/validated | Contract traced | Notes |
|---|---:|---|---|---|
| Unity metadata | 175 | complete | traced | zero missing pairs; zero duplicate GUIDs |
| C# assemblies | 3 | complete | traced | runtime/editor/test boundaries |
| CLI help | 37 | complete | traced | command names, flags, defaults, examples |
| Public/rule docs | 79 | complete | traced | code claims and generated mirrors |
| Install/release/workflows | 13 | complete | traced | platform parity and release guards |

## Passes

- Structural and contract reconstruction: complete
- Adversarial correctness and safety: complete
- Consistency, tests, and omission search: complete
- Zero unread/untraced completion gate: complete

## Findings

1. `AgentConnector/Editor/Tools/Build.cs` constructed `BuildPlayerOptions`
   without the persisted development/debugging/scripts-only flags. The shared
   construction path now maps all three and is covered by `BuildToolTests`.
2. `AgentConnector/Editor/Tools/ReadConsole.cs` returned the oldest entries on
   an initial bounded read. Only the no-cursor path now takes the newest matches;
   explicit cursors preserve forward pagination. `ReadConsoleTests` covers both.
3. `internal/mcpserver/tasks.go` cancelled only local MCP task state for live
   Unity test runs. It now calls the existing `run_tests/cancel` surface and
   keeps package-task cancellation explicitly unsupported.
4. `AgentConnector/Editor/Core/ToolContractSchemaBuilder.cs` emitted required
   fields in compiler metadata order. Partial-class ordering differed across
   Unity 6000.0 and 6000.5, producing different catalog hashes for identical
   contracts. Required fields are now ordinal-sorted and the three live catalogs
   share `sha256:0daec337...`.
5. Official Pipeline gaps were implemented through existing tools where
   possible and one optional reflection-only `manage_timeline` tool where not.
   The final 153-row decision record is in `UNITY_PIPELINE_PARITY_MATRIX.md`.
6. Whole-source duplicate/orphan checks found zero identical Go/C# files and no
   definition-only private/unexported methods. Removed dead palette/key-map
   fields, unused imports/parameters, impossible nil branches, and consolidated
   duplicate asset-config JSON marshaling. The 41 empty catches are bounded
   best-effort cleanup/probe paths or test teardown; none hides a primary result.
7. Project Auditor stays conditional: 6000.0/6000.3 have no module and the
   6000.5 fixture has no rules package. Production code was not added without a
   positive rules-enabled fixture.
