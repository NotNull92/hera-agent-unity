package cmd

import (
	"embed"
	"fmt"
)

//go:embed help/*
var helpFS embed.FS

func printHelp() {
	fmt.Printf("hera-agent-unity %s — Control Unity Editor from the command line\n\n", Version)
	data, _ := helpFS.ReadFile("help/general.txt")
	fmt.Print(string(data))
}

func printTopicHelp(topic string) {
	switch topic {
	case "asset-config":
		printAssetConfigHelp()
		return
	}
	data, err := helpFS.ReadFile("help/" + topic + ".txt")
	if err != nil {
		fmt.Printf("Unknown help topic: %s\n\nUse \"hera-agent-unity --help\" for available commands.\n", topic)
		return
	}
	fmt.Print(string(data))
}
