namespace ManyWinters.Audio;

// What is being said, as a shape rather than as words: no phoneme sequence and no lexicon, because
// intelligibility is the one thing VoiceModel must avoid. Pace is syllables per second; Energy in
// [0, 1] runs from a mutter under the breath to a call across a clearing.
public readonly record struct Utterance(int SyllableCount, float Pace, float Energy);
