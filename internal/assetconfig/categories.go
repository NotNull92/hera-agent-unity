package assetconfig

// CategoryNames maps a machine category key to its human-readable label.
var CategoryNames = map[string]string{
	"inspector":     "Inspector",
	"validation":    "Validation",
	"serialization": "Serialization",
	"animation":     "Animation",
}

// CategoryOrder defines the stable display order for asset categories.
var CategoryOrder = []string{"inspector", "validation", "serialization", "animation"}
