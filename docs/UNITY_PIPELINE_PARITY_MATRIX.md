# Unity Pipeline parity matrix

Baseline: Unity CLI `1.0.0-beta.3`, `com.unity.pipeline@0.5.0-exp.1`.
Generated from 161 source-declared commands; eight internal test commands are excluded, leaving **153 public commands**.

Classification meanings: `covered` is available in Hera, `duplicate` is intentionally served by an existing safer primitive, `rejected` is a locked decision, `excluded` would expand the architecture, and `conditional` requires a positive fixture.

The "Hera equivalent" column names the live catalog identity — `<tool> <action>` — so a row can be checked against `list --catalog` without interpretation. Where the CLI spells the same capability differently, the CLI form follows in parentheses. Three rows name a CLI-only surface (`status`, `editor refresh --compile`) that has no catalog tool.

| Official command | Classification | Hera equivalent or decision |
|---|---|---|
| `add_animator_layer` | covered | manage_animation add_layer |
| `add_animator_parameter` | covered | manage_animation add_parameter |
| `add_animator_state` | covered | manage_animation add_state |
| `add_animator_transition` | covered | manage_animation add_transition |
| `add_component` | covered | manage_components add |
| `add_scene_to_build` | covered | build add_scene |
| `add_timeline_clip` | covered | manage_timeline add_clip |
| `add_timeline_track` | covered | manage_timeline add_track |
| `apply_prefab_overrides` | covered | manage_prefab apply |
| `attach_script` | covered | manage_components add |
| `audit` | conditional | 6000.0/6000.3 expose no Project Auditor assembly; 6000.5+ exposes `UnityEditor.ProjectAuditorModule` but none of the three fixtures has `com.unity.project-auditor-rules`, so no positive rules-enabled fixture exists |
| `audit_status` | conditional | same prerequisite evidence as `audit`; production code intentionally omitted until a rules-enabled fixture can prove non-empty modules/results |
| `bake_lighting` | covered | bake start lighting |
| `bake_navmesh` | covered | bake start navmesh |
| `bake_navmesh_surfaces` | covered | bake start navmesh_surfaces |
| `bake_occlusion_culling` | covered | bake start occlusion |
| `build` | covered | build start carries persisted development/debugging/scripts-only options |
| `build_status` | covered | build status |
| `cancel_lighting_bake` | covered | bake cancel lighting |
| `cancel_navmesh_bake` | covered | bake cancel navmesh |
| `cancel_occlusion_bake` | covered | bake cancel occlusion |
| `cancel_tests` | covered | run_tests cancel (CLI `test cancel`) |
| `capture_editor_element` | covered | screenshot --editor_ui_only returns bounded UI Toolkit metadata; official PNG capture is compiled only for Unity 6000.7+, outside the current 6000.0/6000.3/6000.5+ matrix |
| `capture_game_view` | covered | screenshot --view game |
| `capture_runtime_element` | excluded | runtime/hot-reload server expands locked Editor-only architecture |
| `capture_scene_view` | covered | screenshot |
| `cleanup_hotreload` | excluded | runtime/hot-reload server expands locked Editor-only architecture |
| `clear_baked_lighting` | covered | bake clear lighting |
| `clear_console` | covered | console --clear |
| `clear_navmesh` | covered | bake clear navmesh |
| `clear_occlusion_culling` | covered | bake clear occlusion |
| `console` | covered | console |
| `copy_asset` | covered | manage_assets copy |
| `create_animation_clip` | covered | manage_animation create_clip |
| `create_animator_controller` | covered | manage_animation create_controller |
| `create_asset` | covered | manage_assets create |
| `create_folder` | covered | manage_assets create-folder |
| `create_gameobject` | covered | manage_gameobject create |
| `create_gameobjects` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `create_prefab` | covered | manage_prefab create |
| `create_prefab_variant` | covered | manage_prefab create from prefab instance |
| `create_scene` | covered | scene create |
| `create_script` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `create_timeline` | covered | manage_timeline create |
| `delete_asset` | covered | manage_assets delete |
| `delete_gameobject` | covered | manage_gameobject destroy |
| `editor_focus` | covered | manage_editor focus (Unity EditorWindow focus; not physical OS focus) |
| `editor_pause` | covered | manage_editor pause (CLI `editor pause`) |
| `editor_play` | covered | manage_editor play (CLI `editor play`) |
| `editor_status` | covered | status |
| `editor_stop` | covered | manage_editor stop (CLI `editor stop`) |
| `eval` | covered | exec |
| `eval_file` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `find_assets` | covered | manage_assets find |
| `find_gameobjects` | covered | find_gameobjects |
| `get_animation_clip` | covered | manage_animation get_clip |
| `get_animator_controller` | covered | manage_animation get_controller |
| `get_audio_settings` | covered | manage_settings get_audio |
| `get_authoring_root` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `get_build_settings` | covered | build get_settings |
| `get_component_properties` | covered | manage_components get |
| `get_console_logs` | covered | console |
| `get_graphics_settings` | covered | manage_settings get_graphics |
| `get_import_settings` | covered | manage_asset_import get |
| `get_input_settings` | covered | manage_settings get_input (legacy Input Manager axes) |
| `get_lighting_settings` | covered | manage_settings get_lighting |
| `get_material_properties` | covered | manage_material get |
| `get_navmesh_settings` | covered | manage_settings get_navmesh (legacy NavMesh settings) |
| `get_performance_stats` | covered | profiler stats |
| `get_physics_settings` | covered | manage_settings get_physics |
| `get_player_settings` | covered | manage_settings get_player |
| `get_quality_settings` | covered | manage_settings get_quality |
| `get_scene_hierarchy` | covered | scene hierarchy |
| `get_selection` | covered | manage_editor get_selection |
| `get_serialized_fields` | covered | manage_components get |
| `get_shader_properties` | covered | describe_shader |
| `get_tags_layers` | covered | manage_editor get_tags_layers |
| `get_time_settings` | covered | manage_settings get_time |
| `get_timeline` | covered | manage_timeline get |
| `hotreload_status` | excluded | runtime/hot-reload server expands locked Editor-only architecture |
| `import_asset` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `instantiate_prefab` | covered | manage_prefab instantiate |
| `lighting_bake_status` | covered | bake status lighting |
| `list_build_profiles` | rejected | locked decision or unsafe/duplicative host operation |
| `list_build_targets` | covered | build list_targets |
| `list_open_scenes` | covered | scene list/info |
| `list_shaders` | covered | describe_shader --list |
| `list_tests` | covered | run_tests list (CLI `test list`) |
| `log` | covered | log |
| `menu` | covered | menu |
| `move_asset` | covered | manage_assets move |
| `navmesh_bake_status` | covered | bake status navmesh |
| `occlusion_bake_status` | covered | bake status occlusion |
| `open_scene` | covered | scene load |
| `package_add` | covered | manage_packages add |
| `package_list` | covered | manage_packages list |
| `package_remove` | covered | manage_packages remove |
| `package_resolve` | rejected | locked decision or unsafe/duplicative host operation |
| `package_search` | covered | manage_packages search |
| `package_status` | covered | manage_packages/task status |
| `quit` | rejected | locked decision or unsafe/duplicative host operation |
| `read_text_file` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `recompile` | covered | editor refresh --compile |
| `recompile_status` | covered | status |
| `reload_file` | excluded | runtime/hot-reload server expands locked Editor-only architecture |
| `reload_file_override` | excluded | runtime/hot-reload server expands locked Editor-only architecture |
| `remove_animation_curve` | covered | manage_animation remove_curve |
| `remove_component` | covered | manage_components remove |
| `remove_scene_from_build` | covered | build remove_scene |
| `rename_asset` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `rename_gameobject` | covered | manage_gameobject set_name |
| `revert_prefab_overrides` | covered | manage_prefab revert |
| `run_tests` | covered | run_tests (CLI `test`) |
| `runtime_status` | excluded | runtime/hot-reload server expands locked Editor-only architecture |
| `save_all` | covered | scene save_all |
| `save_prefab_contents` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `save_scene` | covered | scene save |
| `screenshot` | covered | screenshot |
| `search` | rejected | locked decision or unsafe/duplicative host operation |
| `set_active` | covered | manage_gameobject set_active |
| `set_active_scene` | covered | scene set_active |
| `set_animation_curve` | covered | manage_animation set_curve |
| `set_audio_settings` | covered | manage_settings set_audio |
| `set_authoring_root` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `set_autotick` | rejected | locked decision or unsafe/duplicative host operation |
| `set_build_settings` | covered | build set_settings |
| `set_component_properties` | covered | manage_components set |
| `set_graphics_settings` | covered | manage_settings set_graphics |
| `set_import_settings` | covered | manage_asset_import set |
| `set_input_settings` | covered | manage_settings set_input (legacy Input Manager axis tuning) |
| `set_layer` | covered | manage_gameobject set_layer |
| `set_lighting_settings` | covered | manage_settings set_lighting |
| `set_material_properties` | covered | manage_material set |
| `set_navmesh_settings` | covered | manage_settings set_navmesh (legacy NavMesh settings) |
| `set_parent` | covered | manage_gameobject set_parent |
| `set_physics_settings` | covered | manage_settings set_physics |
| `set_player_settings` | covered | manage_settings set_player |
| `set_quality_settings` | covered | manage_settings set_quality |
| `set_selection` | covered | manage_editor set_selection |
| `set_serialized_field` | covered | manage_components set |
| `set_tag` | covered | manage_gameobject set_tag |
| `set_tags_layers` | covered | manage_editor add_tag / remove_tag / add_layer / remove_layer |
| `set_target_framerate` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `set_time_settings` | covered | manage_settings set_time |
| `set_timescale` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |
| `set_transform` | covered | manage_gameobject set_transform |
| `simulate_key` | covered | input keyboard |
| `simulate_pointer` | covered | input mouse/click |
| `switch_build_target` | rejected | locked decision or unsafe/duplicative host operation |
| `switch_build_target_status` | rejected | locked decision or unsafe/duplicative host operation |
| `test_status` | covered | run_tests (CLI `test --resume`) / task status |
| `unpack_prefab` | covered | manage_prefab unpack |
| `write_text_file` | duplicate | existing exec/filesystem/atomic tool already covers the workflow |

## Internal commands excluded

- `job_test_cancellable`
- `job_test_delayed_progress`
- `job_test_wait`
- `log_editor`
- `progress_test_wait`
- `test_structured`
- `test_tagged`
- `test_types`

## Acceptance

Final classification: **126 covered, 12 duplicate, 7 rejected, 6 excluded, 2 conditional**. No `planned` row remains. The conditional Project Auditor rows are not called implemented from absence-only tests.
