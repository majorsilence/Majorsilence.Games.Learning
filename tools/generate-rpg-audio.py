#!/usr/bin/env python3
"""Generates the original soundtrack and sound effects for the RPG.

Everything here is composed and synthesised from scratch in the voice of the
NES sound chip (2A03) - two pulse channels, one triangle, one noise. That is
the shape of the sound of the era: no samples, no filters, four voices, and
whatever you can do with them. None of it is anyone else's music.

Outputs, all mono 22050Hz 16-bit to match the rest of the repo's audio:

  assets/audio/rpg/ashholt.wav   - the town: settled, a little wary
  assets/audio/rpg/hearth.wav    - inn interior: slow, warm, no drums
  assets/audio/rpg/market.wav    - store interior: brisk and businesslike
  assets/audio/rpg/valeroad.wav  - the overworld road: marching, minor, open
  assets/audio/rpg/confirm.wav   - menu/dialogue accept
  assets/audio/rpg/cancel.wav    - menu/dialogue back out
  assets/audio/rpg/door.wav      - passing through a doorway

Each music track is written to loop cleanly: the last bar resolves onto the
downbeat the first bar starts from, and nothing is left ringing at the end.
"""

import math
import os
import struct
import wave

SAMPLE_RATE = 22050

# ---------------------------------------------------------------- notes ----

STEPS = {"c": 0, "d": 2, "e": 4, "f": 5, "g": 7, "a": 9, "b": 11}


def pitch(name):
    """'a4' / 'c#5' / 'eb3' -> Hz. 'r' is a rest and returns None."""
    if name == "r":
        return None
    letter, rest = name[0].lower(), name[1:]
    semitone = STEPS[letter]
    while rest and rest[0] in "#b":
        semitone += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    octave = int(rest)
    # MIDI 69 = A4 = 440Hz
    midi = (octave + 1) * 12 + semitone
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def parse(seq):
    """'a4:2 r:1 c5:.5' -> [(name, beats)]. Bar lines '|' are decoration."""
    out = []
    for token in seq.replace("|", " ").split():
        name, _, beats = token.partition(":")
        out.append((name, float(beats) if beats else 1.0))
    return out


# --------------------------------------------------------------- voices ----
#
# Each voice is a function of (phase-in-cycles, t-in-note, length) -> sample in
# -1..1. Phase is tracked by the caller so a note never starts mid-cycle with a
# click.


def pulse(duty):
    """A square wave with an adjustable high/low split - the 2A03's two melodic voices.

    12.5% is thin and reedy, 25% is the classic NES lead, 50% is a hollow flute.
    """
    def voice(phase, _t, _length):
        return 1.0 if (phase % 1.0) < duty else -1.0
    return voice


def triangle(phase, _t, _length):
    """The NES bass voice: a triangle quantised to 16 levels, which is what gives it its slightly gritty edge."""
    x = phase % 1.0
    value = 4.0 * x - 1.0 if x < 0.5 else 3.0 - 4.0 * x
    return math.floor(value * 8.0) / 8.0


class Noise:
    """The 2A03 noise channel: a 15-bit shift register tapped for pseudo-random 1s and 0s.

    Deterministic, so a rebuild produces byte-identical percussion.
    """

    def __init__(self):
        self.register = 1

    def reset(self):
        self.register = 1

    def step(self):
        feedback = (self.register & 1) ^ ((self.register >> 1) & 1)
        self.register = (self.register >> 1) | (feedback << 14)
        return 1.0 if self.register & 1 else -1.0


# ------------------------------------------------------------ envelopes ----


def envelope(kind, t, length):
    """Volume over the life of one note, 0..1."""
    if kind == "flat":
        # a hair of attack and release, purely to stop the edges clicking
        edge = 0.004
        if t < edge:
            return t / edge
        if t > length - edge:
            return max(0.0, (length - t) / edge)
        return 1.0
    if kind == "decay":
        return max(0.0, 1.0 - t / length)
    if kind == "pluck":
        return math.exp(-t * 9.0)
    if kind == "swell":
        return min(1.0, t * 6.0) * max(0.0, 1.0 - (t / length) ** 3)
    raise ValueError(kind)


# ---------------------------------------------------------------- mixer ----


def render(buffer, seq, bpm, voice, gain, env="flat", gap=0.06, octave=0, start_beat=0.0):
    """Lays a melodic line into an existing float buffer.

    `gap` shortens every note slightly so repeated pitches articulate instead of
    running together - the NES did this with the envelope, we do it with silence.
    """
    beat_seconds = 60.0 / bpm
    cursor = start_beat * beat_seconds
    phase = 0.0

    for name, beats in parse(seq):
        length = beats * beat_seconds
        frequency = pitch(name)
        if frequency is not None:
            frequency *= 2.0 ** octave
            sounding = max(0.0, length - gap)
            first = int(cursor * SAMPLE_RATE)
            count = int(sounding * SAMPLE_RATE)
            step = frequency / SAMPLE_RATE
            phase = 0.0
            for i in range(count):
                index = first + i
                if index >= len(buffer):
                    break
                t = i / SAMPLE_RATE
                buffer[index] += voice(phase, t, sounding) * gain * envelope(env, t, sounding)
                phase += step
        cursor += length


def render_drums(buffer, seq, bpm, gain, start_beat=0.0):
    """Percussion from the noise channel: k kick, s snare, h hat, r rest."""
    beat_seconds = 60.0 / bpm
    cursor = start_beat * beat_seconds
    noise = Noise()

    # (decay rate, how often the shift register is clocked, level)
    kinds = {
        "k": (26.0, 40, 1.0),
        "s": (17.0, 2, 0.7),
        "h": (55.0, 1, 0.35),
    }

    for name, beats in parse(seq):
        if name in kinds:
            decay, divider, level = kinds[name]
            first = int(cursor * SAMPLE_RATE)
            count = int(min(beats * beat_seconds, 0.35) * SAMPLE_RATE)
            noise.reset()
            value = 0.0
            for i in range(count):
                index = first + i
                if index >= len(buffer):
                    break
                if i % divider == 0:
                    value = noise.step()
                t = i / SAMPLE_RATE
                buffer[index] += value * gain * level * math.exp(-t * decay)
        cursor += beats * beat_seconds


def write(path, buffer, peak=0.85):
    """Normalises to a fixed peak and writes 16-bit mono - loud enough to hear, with headroom left."""
    loudest = max((abs(sample) for sample in buffer), default=0.0)
    scale = (peak / loudest) if loudest > 0 else 0.0
    frames = bytearray()
    for sample in buffer:
        value = int(max(-1.0, min(1.0, sample * scale)) * 32767)
        frames += struct.pack("<h", value)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(SAMPLE_RATE)
        out.writeframes(bytes(frames))
    return path


def beats_of(seq):
    return sum(beats for _, beats in parse(seq))


def repeat(pattern, beats):
    """Repeats a drum pattern until it covers `beats` - so percussion can't fall short of the tune."""
    return pattern * max(1, math.ceil(beats / beats_of(pattern)))


def buffer_for(seqs, bpm, tail=0.0):
    """Sizes the buffer from the music itself.

    Declaring a bar count by hand and writing a different number of bars is a
    silent failure - the track still renders, it just has a hole in it, or gets
    chopped off mid-note. Measuring the longest voice makes that impossible.
    `tail` is extra room for a decay to finish, which sound effects need and
    looping music must not have.
    """
    beats = max(beats_of(seq) for seq in seqs)
    return [0.0] * int((beats * (60.0 / bpm) + tail) * SAMPLE_RATE)


# ---------------------------------------------------------------- music ----


def ashholt():
    """The town. A minor with a lifted third in the B phrase - settled, but not entirely at ease."""
    bpm = 108

    lead = (
        "e5:1 a5:1  | g5:.5 f5:.5 e5:1 | d5:1 f5:1   | e5:2 "
        "c5:1 e5:1  | d5:.5 c5:.5 b4:1 | a4:1 b4:1   | a4:2 "
        "e5:1 a5:1  | b5:.5 a5:.5 g5:1 | f5:1 a5:1   | g5:2 "
        "f5:1 e5:1  | d5:.5 e5:.5 f5:1 | e5:1 d5:1   | a4:2 "
    )
    counter = (
        "r:2 | c5:1 r:1 | a4:2 | b4:2 "
        "r:2 | a4:1 r:1 | e4:2 | e4:2 "
        "r:2 | e5:1 r:1 | c5:2 | d5:2 "
        "r:2 | b4:1 r:1 | b4:2 | c5:2 "
    )
    bass = (
        "a2:1 a3:1 | a2:1 e3:1 | d2:1 d3:1 | e2:1 e3:1 "
        "a2:1 a3:1 | f2:1 f3:1 | e2:1 e3:1 | a2:1 e3:1 "
        "a2:1 a3:1 | c3:1 g3:1 | d2:1 d3:1 | g2:1 g3:1 "
        "d2:1 d3:1 | b2:1 b3:1 | e2:1 e3:1 | a2:1 e3:1 "
    )
    buf = buffer_for([lead, counter, bass], bpm)
    drums = repeat("k:1 h:.5 h:.5 s:1 h:.5 k:.5 ", beats_of(lead))

    render(buf, lead, bpm, pulse(0.25), 0.30, env="flat")
    render(buf, counter, bpm, pulse(0.125), 0.16, env="decay")
    render(buf, bass, bpm, triangle, 0.42, env="flat", gap=0.03)
    render_drums(buf, drums, bpm, 0.16)
    return buf


def valeroad():
    """The overworld road. D minor, walking pace, a bass that never stops - you are going somewhere."""
    bpm = 132

    lead = (
        "d5:1.5 e5:.5 | f5:1 a5:1 | g5:1.5 f5:.5 | e5:2 "
        "d5:1.5 e5:.5 | f5:1 d5:1 | c5:1.5 d5:.5 | d5:2 "
        "a5:1.5 g5:.5 | f5:1 e5:1 | d5:1.5 c5:.5 | d5:2 "
        "g5:1 f5:1   | e5:1 d5:1 | c5:1.5 e5:.5 | d5:2 "
    )
    counter = (
        "d4:2 | f4:2 | g4:2 | a4:2 "
        "d4:2 | f4:2 | e4:2 | d4:2 "
        "f4:2 | a4:2 | f4:2 | a4:2 "
        "g4:2 | b4:2 | a4:2 | d4:2 "
    )
    # One bar of walking eighths per root, four roots to the phrase. Written as
    # a list of roots rather than repeated literals so the bar count is obvious.
    def bar(root_low, root_high, fifth):
        return f"{root_low}:.5 {root_high}:.5 {fifth}:.5 {root_high}:.5 "

    bass = "".join(bar(*roots) for roots in [
        ("d2", "d3", "a2"), ("d2", "d3", "a2"), ("bb1", "bb2", "f2"), ("a1", "a2", "e2"),
        ("d2", "d3", "a2"), ("d2", "d3", "a2"), ("g1", "g2", "d2"), ("a1", "a2", "e2"),
        ("f2", "f3", "c3"), ("f2", "f3", "c3"), ("d2", "d3", "a2"), ("a1", "a2", "e2"),
        ("g1", "g2", "d2"), ("g1", "g2", "d2"), ("a1", "a2", "e2"), ("d2", "d3", "a2"),
    ])
    buf = buffer_for([lead, counter, bass], bpm)
    drums = repeat("k:1 h:.5 s:.5 h:.5 h:.5 s:.5 h:.5 ", beats_of(lead))

    render(buf, lead, bpm, pulse(0.5), 0.28, env="flat")
    render(buf, counter, bpm, pulse(0.125), 0.14, env="swell")
    render(buf, bass, bpm, triangle, 0.40, env="flat", gap=0.02)
    render_drums(buf, drums, bpm, 0.18)
    return buf


def hearth():
    """The inn. F major, half the tempo of everywhere else, no percussion - the only room in the game that is safe."""
    bpm = 72

    lead = (
        "f4:2 a4:1 c5:1 | d5:2 c5:2 | a4:2 g4:1 a4:1 | f4:4 "
        "c5:2 a4:1 f4:1 | g4:2 bb4:2 | a4:2 g4:2     | f4:4 "
    )
    counter = (
        "r:4 | f4:2 e4:2 | r:4 | c4:4 "
        "r:4 | d4:2 d4:2 | r:4 | c4:4 "
    )
    bass = (
        "f2:2 c3:2 | bb2:2 f2:2 | c2:2 c3:2 | f2:4 "
        "f2:2 a2:2 | bb2:2 g2:2 | c2:2 c3:2 | f2:4 "
    )

    buf = buffer_for([lead, counter, bass], bpm)

    render(buf, lead, bpm, pulse(0.5), 0.30, env="swell")
    render(buf, counter, bpm, pulse(0.25), 0.13, env="decay")
    render(buf, bass, bpm, triangle, 0.40, env="flat", gap=0.04)
    return buf


def market():
    """The store. C major, short, brisk, and slightly too cheerful - shop music is meant to move you along."""
    bpm = 126

    lead = (
        "c5:.5 e5:.5 g5:1 | e5:.5 g5:.5 c6:1 | b5:.5 a5:.5 g5:1 | e5:2 "
        "d5:.5 f5:.5 a5:1 | f5:.5 a5:.5 d6:1 | c6:.5 b5:.5 a5:1 | c5:2 "
    )
    counter = (
        "e4:1 g4:1 | c5:1 g4:1 | d5:1 b4:1 | c5:2 "
        "f4:1 a4:1 | d5:1 a4:1 | e5:1 c5:1 | e4:2 "
    )
    bass = (
        "c2:1 c3:1 | c2:1 g2:1 | g1:1 g2:1 | c2:2 "
        "f2:1 f3:1 | d2:1 d3:1 | g1:1 g2:1 | c2:2 "
    )
    buf = buffer_for([lead, counter, bass], bpm)
    drums = repeat("k:.5 h:.5 s:.5 h:.5 ", beats_of(lead))

    render(buf, lead, bpm, pulse(0.25), 0.30, env="pluck")
    render(buf, counter, bpm, pulse(0.125), 0.15, env="pluck")
    render(buf, bass, bpm, triangle, 0.40, env="flat", gap=0.03)
    render_drums(buf, drums, bpm, 0.14)
    return buf


# ------------------------------------------------------------- effects ----
#
# Short, dry and unmistakable from each other with the music playing over them,
# which matters more than what they sound like in isolation.


# The `tail` is what lets the final pluck decay to silence inside the file. Cut
# it short and the waveform stops at full amplitude, which is an audible click
# every single time the effect plays.
SFX_TAIL = 0.12


def confirm():
    """Two notes up: unmistakably a yes."""
    line = "e5:1 b5:2 "
    buf = buffer_for([line], 460, tail=SFX_TAIL)
    render(buf, line, 460, pulse(0.5), 0.75, env="pluck", gap=0.0)
    return buf


def cancel():
    """The same shape downward: unmistakably a no."""
    line = "b4:1 e4:2 "
    buf = buffer_for([line], 460, tail=SFX_TAIL)
    render(buf, line, 460, pulse(0.25), 0.70, env="pluck", gap=0.0)
    return buf


def door():
    """A latch and a swing: a noise tick over a short rising figure."""
    line = "a3:1 e4:1 a4:2 "
    buf = buffer_for([line], 320, tail=SFX_TAIL)
    render(buf, line, 320, pulse(0.125), 0.55, env="pluck", gap=0.005)
    render_drums(buf, "h:1 ", 320, 0.5)
    return buf


TRACKS = {
    "ashholt": ashholt,
    "valeroad": valeroad,
    "hearth": hearth,
    "market": market,
    "confirm": confirm,
    "cancel": cancel,
    "door": door,
}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.join(here, "..", "Majorsilence.Games.Rpg", "assets", "audio", "rpg")

    for name, build in TRACKS.items():
        path = write(os.path.join(out_dir, name + ".wav"), build())
        with wave.open(path) as check:
            seconds = check.getnframes() / check.getframerate()
        print(f"{os.path.normpath(path)}  {seconds:.1f}s")


if __name__ == "__main__":
    main()
