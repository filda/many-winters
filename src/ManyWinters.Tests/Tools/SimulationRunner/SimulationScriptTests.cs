using ManyWinters.Tools.SimulationRunner;

namespace ManyWinters.Tests.Tools.SimulationRunner;

public class SimulationScriptTests
{
    [Fact]
    public void SplitIntoCommandsGroupsUnquotedArgvByVerb()
    {
        var commands = SimulationScript.SplitIntoCommands(["create", "2", "simulate", "100", "print", "population"]);

        Assert.Equal(["create 2", "simulate 100", "print population"], commands);
    }

    [Fact]
    public void SplitIntoCommandsHandlesASingleVerbWithNoArguments()
    {
        var commands = SimulationScript.SplitIntoCommands(["generate"]);

        Assert.Equal(["generate"], commands);
    }

    [Fact]
    public void SplitIntoCommandsRecognizesEveryVerbAsASplitBoundary()
    {
        var commands = SimulationScript.SplitIntoCommands([
            "generate", "create", "2", "simulate", "5", "print", "population", "save", "a.json", "load", "b.json",
        ]);

        Assert.Equal(
            ["generate", "create 2", "simulate 5", "print population", "save a.json", "load b.json"],
            commands);
    }

    [Fact]
    public void SplitIntoCommandsKeepsNonVerbTokensTogetherAsOneUnknownCommand()
    {
        var commands = SimulationScript.SplitIntoCommands(["fly", "to", "the", "moon"]);

        Assert.Equal(["fly to the moon"], commands);
    }

    [Fact]
    public void SplitIntoCommandsOnEmptyInputProducesNoCommands()
    {
        var commands = SimulationScript.SplitIntoCommands([]);

        Assert.Empty(commands);
    }

    [Fact]
    public void CreateTwoViaUnquotedArgvActuallyCreatesTwoPeople()
    {
        var script = new SimulationScript();

        var output = script.Run(SimulationScript.SplitIntoCommands(["create", "2"]));

        Assert.Equal(2, script.World.People.Count);
        Assert.Contains("Created 2 people. Population is now 2.", output);
    }

    [Fact]
    public void StartsWithAFreshEmptyWorld()
    {
        var script = new SimulationScript();

        Assert.Empty(script.World.People);
        Assert.Equal(0, script.World.Clock.CurrentTick);
    }

    [Fact]
    public void CreateAddsPeopleAndReportsPopulation()
    {
        var script = new SimulationScript();

        var output = script.Run(["create 3"]);

        Assert.Equal(3, script.World.People.Count);
        Assert.Contains("Created 3 people. Population is now 3.", output);
    }

    [Fact]
    public void CreateWithoutAValidCountReportsUsageAndAddsNoOne()
    {
        var script = new SimulationScript();

        var output = script.Run(["create banana"]);

        Assert.Empty(script.World.People);
        Assert.Contains("Invalid command: 'create banana'. Usage: create <n>", output);
    }

    [Fact]
    public void SimulateAdvancesTheClock()
    {
        var script = new SimulationScript();

        var output = script.Run(["simulate 5"]);

        Assert.Equal(5, script.World.Clock.CurrentTick);
        Assert.Contains("Advanced 5 ticks. Current tick is 5.", output);
    }

    [Fact]
    public void CreateWithoutACountArgumentReportsUsage()
    {
        var script = new SimulationScript();

        var output = script.Run(["create"]);

        Assert.Empty(script.World.People);
        Assert.Contains("Invalid command: 'create'. Usage: create <n>", output);
    }

    [Fact]
    public void SimulateWithoutAValidTickCountReportsUsage()
    {
        var script = new SimulationScript();

        var output = script.Run(["simulate banana"]);

        Assert.Equal(0, script.World.Clock.CurrentTick);
        Assert.Contains("Invalid command: 'simulate banana'. Usage: simulate <ticks>", output);
    }

    [Fact]
    public void SimulateWithoutATickCountArgumentReportsUsage()
    {
        var script = new SimulationScript();

        var output = script.Run(["simulate"]);

        Assert.Equal(0, script.World.Clock.CurrentTick);
        Assert.Contains("Invalid command: 'simulate'. Usage: simulate <ticks>", output);
    }

    [Fact]
    public void BlankCommandProducesNoOutput()
    {
        var script = new SimulationScript();

        var output = script.Run(["   "]);

        Assert.Empty(output);
    }

    [Fact]
    public void GenerateReplacesTheCurrentWorld()
    {
        var script = new SimulationScript();
        script.Run(["create 2", "simulate 5"]);

        var output = script.Run(["generate"]);

        Assert.Empty(script.World.People);
        Assert.Equal(0, script.World.Clock.CurrentTick);
        Assert.Contains("Generated a new world.", output);
    }

    [Fact]
    public void PrintPopulationListsEveryPerson()
    {
        var script = new SimulationScript();
        script.Run(["create 2"]);

        var output = script.Run(["print population"]);

        Assert.Contains("Tick 0: 2 people alive.", output);
        Assert.Contains(output, line => line.Contains("Person 1"));
        Assert.Contains(output, line => line.Contains("Person 2"));
    }

    [Fact]
    public void UnknownCommandIsReportedAndDoesNotThrow()
    {
        var script = new SimulationScript();

        var output = script.Run(["fly to the moon"]);

        Assert.Contains("Unknown command: 'fly to the moon'.", output);
    }

    [Fact]
    public void PrintWithoutPopulationArgumentIsUnknownCommand()
    {
        var script = new SimulationScript();

        var output = script.Run(["print"]);

        Assert.Contains("Unknown command: 'print'.", output);
    }

    [Fact]
    public void SaveWithoutAPathIsUnknownCommand()
    {
        var script = new SimulationScript();

        var output = script.Run(["save"]);

        Assert.Contains("Unknown command: 'save'.", output);
    }

    [Fact]
    public void LoadWithoutAPathIsUnknownCommand()
    {
        var script = new SimulationScript();

        var output = script.Run(["load"]);

        Assert.Contains("Unknown command: 'load'.", output);
    }

    [Fact]
    public void SaveThenLoadRoundTripsThroughCommands()
    {
        var script = new SimulationScript();
        script.Run(["create 2", "simulate 7"]);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-scripttest-{Guid.NewGuid():N}.json");
        try
        {
            var saveOutput = script.Run([$"save {path}"]);
            Assert.Contains($"Saved to {path}.", saveOutput);

            var freshScript = new SimulationScript();
            var loadOutput = freshScript.Run([$"load {path}"]);

            Assert.Equal(7, freshScript.World.Clock.CurrentTick);
            Assert.Equal(2, freshScript.World.People.Count);
            Assert.Contains($"Loaded from {path}. Tick 7, 2 people.", loadOutput);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
