using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// What the player is offered for the thing they pointed at. Same rules as PersonActions: nothing
// is offered without something to act on, and every offer carries where it happens so a refusal
// for distance alone turns into a walk.
public class TargetActionsTests
{
    private static readonly Position Camp = new(0, 0);
    private static readonly Position FarAway = new(50, 0);

    private static ResourceNode AddNode(WorldState world, ResourceKindId kind, Position position)
    {
        var node = new ResourceNode { Kind = kind, Position = position, RemainingAmount = 100, MaxAmount = 100 };
        world.AddResourceNode(node);
        return node;
    }

    private static Person AddCorpse(WorldState world, string name, Position position)
    {
        var person = TestWorld.AddAdult(world, name, position);
        person.IsAlive = false;
        return person;
    }

    private static List<string> Labels(TargetMenu menu) => menu.Offers.Select(offer => offer.Label).ToList();

    private static ActionOffer Labelled(TargetMenu menu, string label) =>
        Assert.Single(menu.Offers, offer => offer.Label == label);

    // A tree is named on the heading, so the lines under it are bare verbs rather than a column
    // repeating what was pointed at.
    [Fact]
    public void AResourceIsHeadedByItsOwnName()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Equal("Apple", TargetActions.For(world, person, AddNode(world, TestWorld.AppleTree, Camp)).Heading);
    }

    [Fact]
    public void AFellableResourceCanBeGatheredFromOrFelled()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Equal(["Gather", "Fell"], Labels(TargetActions.For(world, person, AddNode(world, TestWorld.AppleTree, Camp))));
    }

    // Felling is a property of the kind, not of the moment: a standing greyed-out "Fell" on every
    // mushroom is a line the player learns to ignore.
    [Fact]
    public void SomethingThatCannotBeFelledIsNotOfferedFelling()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Equal(["Gather"], Labels(TargetActions.For(world, person, AddNode(world, TestWorld.Stump, Camp))));
    }

    // Pointing at the tree is how the person is shown what to do with it (see
    // SkillDefinition.BaseTechnique), so never having learned cannot be what stops the offer.
    [Fact]
    public void GatheringAndFellingTeachTheirOwnSkill()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var menu = TargetActions.For(world, person, AddNode(world, TestWorld.AppleTree, Camp));

        Assert.Empty(person.KnownTechniques);
        Assert.All(menu.Offers, offer => Assert.NotNull(offer.TeachFirst));
        Assert.All(menu.Offers, offer => Assert.True(offer.IsAvailable));
    }

    // The whole point of carrying the target: a tree across the clearing is somewhere the person
    // can be sent, so the offer stands rather than telling the player to walk them over first.
    [Fact]
    public void AResourceOutOfReachIsSomethingToWalkTo()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, TestWorld.AppleTree, FarAway);

        var gather = TargetActions.Gather(world, person, node);

        Assert.Equal(ActionBlocker.TooFar, gather.Blocker);
        Assert.Equal(node.Position, gather.Target);
        Assert.True(gather.NeedsWalkingTo);
        Assert.True(gather.IsAvailable);
    }

    // A refusal that walking will not mend stays a refusal.
    [Fact]
    public void AResourceWithNothingLeftIsRefusedRatherThanWalkedTo()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, TestWorld.AppleTree, Camp);
        node.RemainingAmount = 0;

        var gather = TargetActions.Gather(world, person, node);

        Assert.Equal(ActionBlocker.NothingLeft, gather.Blocker);
        Assert.False(gather.IsAvailable);
    }

    [Fact]
    public void GatherIsTheSameOfferWhicheverWayItIsAskedFor()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, TestWorld.AppleTree, Camp);

        Assert.Equal(TargetActions.Gather(world, person, node), TargetActions.For(world, person, node).Offers[0]);
    }

    // A lesson is one technique, the way the band's own casual teaching hands over at most one
    // per tick (WorldState.AutoTeachNearbyPeople) - so a teacher who knows two things offers two
    // lessons, and the player picks which.
    [Fact]
    public void ALivingPersonIsOfferedALessonPerThingTheTeacherCouldPassOn()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.KnownTechniques.Add(TestWorld.BasicForaging);
        ava.KnownTechniques.Add(TestWorld.BasicTeaching);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);

        var menu = TargetActions.For(world, ava, bran);

        Assert.Equal("Bran", menu.Heading);
        Assert.Equal(["Teach foraging", "Teach teaching", "Have a child"], Labels(menu));
    }

    [Fact]
    public void ALessonCarriesTheOneTechniqueItIsNamedAfter()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.KnownTechniques.Add(TestWorld.BasicForaging);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);

        var lesson = Labelled(TargetActions.For(world, ava, bran), "Teach foraging");

        Assert.Equal(TestWorld.BasicForaging, Assert.IsType<TeachCommand>(lesson.Command).Technique);
    }

    // Nothing the teacher does not know themselves, and nothing the student already has: what is
    // left is exactly the list worth drawing.
    [Fact]
    public void NobodyIsOfferedALessonInWhatTheyAlreadyKnow()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.KnownTechniques.Add(TestWorld.BasicForaging);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);
        bran.KnownTechniques.Add(TestWorld.BasicForaging);

        Assert.Equal(["Have a child"], Labels(TargetActions.For(world, ava, bran)));
    }

    [Fact]
    public void ATeacherWhoKnowsNothingHasNoLessonToGive()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);

        Assert.Equal(["Have a child"], Labels(TargetActions.For(world, ava, bran)));
    }

    // An efficient technique is worked out by doing the thing over and over
    // (SkillDefinition.EfficientTechnique), so it cannot be handed over - the band's own casual
    // teaching refuses to pass one on for the same reason.
    [Fact]
    public void WhatWasWorkedOutByPractiseCannotBeHandedOver()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.KnownTechniques.Add(TestWorld.BasicForaging);
        ava.KnownTechniques.Add(TestWorld.EfficientForaging);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);

        var lessons = TargetActions.For(world, ava, bran).Offers
            .Where(offer => offer.Command is TeachCommand)
            .Select(offer => Assert.IsType<TeachCommand>(offer.Command).Technique);

        Assert.Equal([TestWorld.BasicForaging], lessons);
    }

    // Burying somebody who is still talking and teaching a corpse are not choices worth drawing,
    // so the living and the dead are offered different things rather than one half-refused list.
    [Fact]
    public void ADeadPersonCanBeBuried()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Equal(["Bury"], Labels(TargetActions.For(world, ava, AddCorpse(world, "Bran", Camp))));
    }

    [Fact]
    public void ADeadPersonCarryingSomethingCanAlsoBeRobbedOfIt()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var bran = AddCorpse(world, "Bran", Camp);
        bran.Inventory.Add(TestWorld.Wood, 3);

        Assert.Equal(["Bury", "Take what they carried"], Labels(TargetActions.For(world, ava, bran)));
    }

    // Whose child it would be follows from who they are, not from who happened to be pointed at.
    [Fact]
    public void TheMotherIsTheWomanWhicheverOfThemWasPointedAt()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);

        var pointedAtHim = Labelled(TargetActions.For(world, ava, bran), "Have a child").Command;
        var pointedAtHer = Labelled(TargetActions.For(world, bran, ava), "Have a child").Command;

        Assert.Equal(ava, Assert.IsType<BirthCommand>(pointedAtHim).Mother);
        Assert.Equal(ava, Assert.IsType<BirthCommand>(pointedAtHer).Mother);
    }

    [Fact]
    public void TwoOfTheSameSexAreToldWhatIsMissingRatherThanNotOfferedIt()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var sela = TestWorld.AddAdult(world, "Sela", Camp);

        Assert.Equal(ActionBlocker.WrongSex, Labelled(TargetActions.For(world, ava, sela), "Have a child").Blocker);
    }

    // Everything a person does to themselves is on their own card already (PersonActions), which
    // is on screen the whole time they are selected.
    [Fact]
    public void PointingAtTheSelectedPersonThemselvesOffersNothing()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Empty(TargetActions.For(world, ava, ava).Offers);
    }

    // Being told to teach is itself the player showing them how to teach (see
    // SkillDefinition.BaseTechnique), so never having learned that cannot be what stops a lesson.
    [Fact]
    public void ALessonIsNotBlockedForTheTeacherNeverHavingBeenTaughtToTeach()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.KnownTechniques.Add(TestWorld.BasicForaging);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);

        var lesson = Labelled(TargetActions.For(world, ava, bran), "Teach foraging");

        Assert.DoesNotContain(TestWorld.BasicTeaching, ava.KnownTechniques);
        Assert.Equal(TeachCommand.TeachingSkill, lesson.TeachFirst);
        Assert.True(lesson.IsAvailable);
    }

    // A store is a list of what it holds: "Put in" with nothing to put in is not a choice, and
    // neither is taking out of an empty hut.
    [Fact]
    public void AnEmptyStoreAndAnEmptyPackLeaveOnlyMending()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Equal(["Mend"], Labels(TargetActions.For(world, ava, TestWorld.AddStorageHut(world, Camp))));
    }

    [Fact]
    public void AStoreOffersALinePerKindThereIsToMoveEitherWay()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.Inventory.Add(TestWorld.Wood, 4);
        var hut = TestWorld.AddStorageHut(world, Camp);
        hut.Inventory.Add(TestWorld.Apple, 2);

        var menu = TargetActions.For(world, ava, hut);

        Assert.Equal("Storage Hut", menu.Heading);
        Assert.Equal(["Put in wood", "Take out apple", "Mend"], Labels(menu));
    }

    [Fact]
    public void PuttingSomethingInMovesEverythingOfThatKindTheyCarry()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.Inventory.Add(TestWorld.Wood, 4);
        var hut = TestWorld.AddStorageHut(world, Camp);

        var deposit = Assert.IsType<DepositCommand>(Labelled(TargetActions.For(world, ava, hut), "Put in wood").Command);

        Assert.Equal(4, deposit.Amount);
    }

    [Fact]
    public void ASoundHutSaysThereIsNothingToMend()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);

        var mend = Labelled(TargetActions.For(world, ava, TestWorld.AddStorageHut(world, Camp)), "Mend");

        Assert.Equal(ActionBlocker.NothingToRepair, mend.Blocker);
    }

    // A pile is always one kind (ItemPile), so the heading already names it and the offer under
    // it is a bare verb - the same shape as a resource's "Gather".
    [Fact]
    public void APileIsHeadedByItsOwnKindAndOffersPickingItUp()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var pile = new ItemPile { Kind = TestWorld.Wood, Position = Camp, Amount = 3 };

        var menu = TargetActions.For(world, ava, pile);

        Assert.Equal("Wood", menu.Heading);
        Assert.Equal(["Pick up"], Labels(menu));
        Assert.Equal(TestWorld.Wood, Assert.IsType<PickUpItemCommand>(menu.Offers[0].Command).Pile.Kind);
    }

    [Fact]
    public void APileOutOfReachIsSomethingToWalkTo()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var pile = new ItemPile { Kind = TestWorld.Wood, Position = FarAway, Amount = 3 };

        var pickUp = TargetActions.PickUp(world, ava, pile);

        Assert.Equal(ActionBlocker.TooFar, pickUp.Blocker);
        Assert.Equal(pile.Position, pickUp.Target);
        Assert.True(pickUp.NeedsWalkingTo);
    }

    [Fact]
    public void PickUpIsTheSameOfferWhicheverWayItIsAskedFor()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var pile = new ItemPile { Kind = TestWorld.Wood, Position = Camp, Amount = 3 };

        Assert.Equal(TargetActions.PickUp(world, ava, pile), TargetActions.For(world, ava, pile).Offers[0]);
    }

    [Fact]
    public void BareGroundIsSomewhereToWalkTo()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);

        var menu = TargetActions.For(world, ava, FarAway);

        Assert.Equal("This spot", menu.Heading);
        Assert.Equal(["Walk here"], Labels(menu));
    }

    // Building needs a place chosen rather than a thing pointed at, so it lives on the ground's
    // own menu - offered from the first unit of the material, with the blocker saying how much
    // more it takes.
    [Fact]
    public void SomebodyCarryingSomeOfTheMaterialIsOfferedTheBuildingAndToldWhatIsMissing()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.Inventory.Add(TestWorld.Wood, 1);

        var menu = TargetActions.For(world, ava, Camp);

        Assert.Equal(["Walk here", "Build storage hut"], Labels(menu));
        Assert.Equal(ActionBlocker.MissingMaterials, Labelled(menu, "Build storage hut").Blocker);
    }

    [Fact]
    public void SomebodyCarryingNoneOfTheMaterialIsNotOfferedTheBuildingAtAll()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);

        Assert.Equal(["Walk here"], Labels(TargetActions.For(world, ava, Camp)));
    }

    [Fact]
    public void BuildingGoesUpWhereThePlayerPointedRatherThanWhereThePersonStands()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.Inventory.Add(TestWorld.Wood, TestWorld.StorageHutInputAmount);

        var build = Labelled(TargetActions.For(world, ava, FarAway), "Build storage hut");

        Assert.Equal(FarAway, Assert.IsType<ConstructCommand>(build.Command).Position);
        Assert.True(build.NeedsWalkingTo);
    }

    // Every offer aimed at something carries where it happens, or the walk that would carry the
    // order has nowhere to go (see ActionOffer.Target, PendingOrders).
    [Fact]
    public void EveryOfferKnowsWhereItHappens()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.Inventory.Add(TestWorld.Wood, 4);
        var bran = TestWorld.AddAdult(world, "Bran", Camp, Sex.Male);
        var corpse = AddCorpse(world, "Tora", Camp);
        corpse.Inventory.Add(TestWorld.Apple, 1);
        var hut = TestWorld.AddStorageHut(world, Camp);
        hut.Inventory.Add(TestWorld.Apple, 2);

        IReadOnlyList<TargetMenu> menus =
        [
            TargetActions.For(world, ava, AddNode(world, TestWorld.AppleTree, Camp)),
            TargetActions.For(world, ava, bran),
            TargetActions.For(world, ava, corpse),
            TargetActions.For(world, ava, hut),
            TargetActions.For(world, ava, FarAway),
        ];

        Assert.All(menus, menu => Assert.All(menu.Offers, offer => Assert.NotNull(offer.Target)));
    }

    [Fact]
    public void TheSameWorldAlwaysOffersTheSameThingsInTheSameOrder()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        ava.Inventory.Add(TestWorld.Wood, 4);
        ava.Inventory.Add(TestWorld.Apple, 2);
        var hut = TestWorld.AddStorageHut(world, Camp);
        hut.Inventory.Add(TestWorld.Wood, 1);
        hut.Inventory.Add(TestWorld.Apple, 1);

        Assert.Equal(
            Labels(TargetActions.For(world, ava, hut)),
            Labels(TargetActions.For(world, ava, hut)));
    }
}
