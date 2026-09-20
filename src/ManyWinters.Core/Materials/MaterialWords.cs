namespace ManyWinters.Core.Materials;

// What a substance is like, in the words somebody handling it would use. The player is meant to
// form a hypothesis and have the simulation rule on it (see
// docs/materials-and-crafting-architecture.md section 7), and a hypothesis needs something to
// go on - but section 9 forbids showing the numbers, so the properties come out as plain
// adjectives instead.
//
// Says what a thing *is*, never what it is *for*: "fibrous, pliable" and never "can be
// twisted". Working that out is the game, and a line that gave it away would be the verb list
// the workshop was built to avoid. In Core rather than beside the rest of the player's prose,
// because the chronicle will want to describe a thing in the same words as the panel does
// (section 4 makes the same point about function scores).
public static class MaterialWords
{
    // High enough that a word means something when it appears - a thing described as hard,
    // fibrous, springy and heavy at once has told the player nothing.
    private const float Marked = 0.6f;
    private const float Faint = 0.3f;

    // Dense enough to be worth noticing in the hand; roughly stone, against water at 1.
    private const float Heavy = 1.5f;
    private const float Light = 0.4f;

    // Ordered as a person would say them - what it is made like first, then how it behaves,
    // then how it sits in the hand - and capped, so the line reads as a description rather
    // than as a stat block spelled out in English.
    private const int MostWords = 3;

    public static IReadOnlyList<string> For(MaterialDefinition material)
    {
        var words = new List<string>();

        if (material.Hardness >= Marked)
        {
            words.Add("hard");
        }
        else if (material.Hardness > 0f && material.Hardness <= Faint)
        {
            words.Add("soft");
        }

        if (material.Fibrousness >= Marked)
        {
            words.Add("fibrous");
        }

        // Both halves are worth saying: that a thing shatters warns somebody about to strike it,
        // and that it endures is why anybody would choose it for a haft.
        if (material.Toughness >= Marked)
        {
            words.Add("tough");
        }
        else if (material.Toughness > 0f && material.Toughness <= Faint)
        {
            words.Add("brittle");
        }

        if (material.Elasticity >= Marked)
        {
            words.Add("springy");
        }
        else if (material.Flexibility >= Marked)
        {
            // Only when it is not springy: springy already tells the player it gives, and says
            // more.
            words.Add("pliable");
        }
        else if (material.Flexibility > 0f && material.Flexibility <= Faint + 0.1f)
        {
            // The other end of the same axis, and the one a haft is chosen for: a thing that
            // will not give is a thing that carries a blow.
            words.Add("stiff");
        }

        if (material.Density >= Heavy)
        {
            words.Add("heavy");
        }
        else if (material.Density > 0f && material.Density <= Light)
        {
            words.Add("light");
        }

        return words.Count > MostWords ? words.GetRange(0, MostWords) : words;
    }
}
