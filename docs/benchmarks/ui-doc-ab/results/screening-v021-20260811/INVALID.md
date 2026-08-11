# Invalid screening wave

This wave is retained as infrastructure evidence only and must not be used for the M2-M5 decision.

Reason: the original disposable fixture copied the full Unity Hub 2D template manifest. After the first two measured cells, unrelated registry packages (`com.unity.test-framework.performance`, 2D Tilemap/2D Common, Rider/Visual Studio integration, and others) entered compile-error states during Editor relaunch. The Hera Connector then could not produce a heartbeat for the `primitives_batch` cell after three launch attempts.

Valid authoring results collected before the fixture failure are intentionally excluded together with the failed cells, because keeping only the early cells would create order/fixture selection bias.

Replacement protocol: `minimal-ugui` fixture with Hera + uGUI + built-in Unity modules only, package tests removed from the disposable Connector snapshot, empty task/reset scenes, and one warm Editor process reused across all cells with hash-verified live Scene reset between fresh Codex sessions.
