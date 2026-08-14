package mcpserver

import (
	"errors"
	"fmt"
)

const (
	ExposureProfile = "profile"
	ExposureCompact = "compact"
	ExposureFull    = "full"
)

var ErrArbitraryCodePermissionRequired = errors.New("arbitrary-code permission is required")

func isSupportedProfile(profile string) bool {
	return isSeedProfile(profile) || profile == "custom" || profile == "full" || profile == "advanced"
}

func (config Config) exposure() string {
	if config.Exposure == "" {
		return ExposureCompact
	}
	return config.Exposure
}

func (config Config) effectiveProfile() string {
	if config.exposure() == ExposureFull {
		return "full"
	}
	return config.Profile
}

func (config Config) validateExposure() error {
	switch config.exposure() {
	case ExposureProfile, ExposureCompact, ExposureFull:
	default:
		return fmt.Errorf("unsupported MCP exposure %q", config.Exposure)
	}
	if !isSupportedProfile(config.Profile) {
		return fmt.Errorf("unsupported MCP profile %q", config.Profile)
	}
	if config.effectiveProfile() == "advanced" && !config.AllowArbitraryCode {
		return fmt.Errorf("%w; pass --allow-arbitrary-code to start the advanced profile", ErrArbitraryCodePermissionRequired)
	}
	return nil
}
