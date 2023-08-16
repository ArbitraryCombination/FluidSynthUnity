using System;
using System.Collections.Generic;
using FluidSynth;
using FluidSynthUnity;
using UnityEngine;

public class SoundTest : MonoBehaviour {

    public SoundFontAsset SoundFontAsset;
    
    public MidiSynthBehavior MidiPlayer;

    private int instrumentIndex = 0;
    private List<(int, int)> Instruments;

    private bool pianoEnabled = false;
    private int transposition = 0;

    private static readonly (Tone, KeyCode)[] TONES = { (Tone.C_4, KeyCode.A), (Tone.CIS_4, KeyCode.W), (Tone.D_4, KeyCode.S), (Tone.DIS_4, KeyCode.E), (Tone.E_4, KeyCode.D), (Tone.F_4, KeyCode.F), (Tone.FIS_4, KeyCode.T), (Tone.G_4, KeyCode.G), (Tone.GIS_4, KeyCode.Z), (Tone.A_4, KeyCode.H), (Tone.AIS_4, KeyCode.U), (Tone.B_4, KeyCode.J), (Tone.C_5, KeyCode.K), (Tone.CIS_5, KeyCode.O), (Tone.D_5, KeyCode.L)};
    private readonly MPTKEvent[] notes = new MPTKEvent[127];

    private void Awake() {
        SoundFontManager.LoadSoundFont(SoundFontAsset);
    }

    private void DemoSelectedInstrument() {
        MidiSynthBehavior player = MidiPlayer;
        (int bank, int num) instr = Instruments[instrumentIndex];
        var instrument = SoundFontManager.soundFont.Instruments[instr];

        pianoEnabled = true;
        Debug.Log("(" + instr.bank + ", " + instr.num + ") "+instrument.Name);
        player.PlayNote(Tone.C_4, instr, 100, 100, 0);
        player.PlayNote(Tone.D_4, instr, 100, 100, 100);
        player.PlayNote(Tone.E_4, instr, 100, 100, 200);
        player.PlayNote(Tone.F_4, instr, 100, 100, 300);
        player.PlayNote(Tone.G_4, instr, 100, 100, 400);
        player.PlayNote(Tone.A_4, instr, 100, 100, 500);
        player.PlayNote(Tone.B_4, instr, 100, 100, 600);
        player.PlayNote(Tone.C_5, instr, 100, 100, 700);
    }

    private void Update() {
        SoundFont soundFont = SoundFontManager.soundFont;
        if (soundFont == null || !SoundFontManager.soundFontInitialized) {
            return;
        }
        MidiSynthBehavior player = MidiPlayer;

        if (Instruments == null) {
            Instruments = new List<(int, int)>(soundFont.Instruments.Keys);
            Instruments.Sort();
        }

        if (Input.GetKeyDown(KeyCode.F6)) {
            transposition -= 1;
            Debug.Log("Transposition: " + transposition);
        } else if (Input.GetKeyDown(KeyCode.F7)) {
            // Previous
            if (--instrumentIndex < 0) {
                instrumentIndex = Instruments.Count - 1;
            }
            DemoSelectedInstrument();
        } else if (Input.GetKeyDown(KeyCode.F8)) {
            DemoSelectedInstrument();
        } else if (Input.GetKeyDown(KeyCode.F9)) {
            // Next
            if (++instrumentIndex >= Instruments.Count) {
                instrumentIndex = 0;
            }
            DemoSelectedInstrument();
        } else if (Input.GetKeyDown(KeyCode.F10)) {
            transposition += 1;
            Debug.Log("Transposition: " + transposition);
        } else if (pianoEnabled) {
            foreach ((Tone rawTone, KeyCode key) in TONES) {
                bool pressed = Input.GetKeyDown(key);
                if (!pressed && !Input.GetKeyUp(key)) continue;

                var toneInt = (int) rawTone + transposition * 12;
                if (toneInt >= 0 && toneInt < notes.Length) {
                    (int bank, int num) instr = Instruments[instrumentIndex];

                    if (pressed) {
                        notes[toneInt] = player.PlayNote((Tone) toneInt, instr, -1, 100);
                    } else {
                        player.StopEvent(notes[toneInt]);
                        notes[toneInt] = null;
                    }
                }
            }
        }
        
    }

}
