
using System;
using System.Collections.Generic;

namespace MidiPlayerTK {
	
	/**
	 * Takes care of synthesizing a single note event,
	 * its modulation and so on, for the whole duration of it making any sound.
	 *
	 * The intended use of this is that it is reused for many uses and reset in-between.
	 * Playing a new note stops the previous one.
	 *
	 * This class is not thread safe.
	 */
	public class MidiSynthesizer {

		private readonly fluid_channel channel = new fluid_channel();

		private readonly List<fluid_voice> activeVoices = new List<fluid_voice>();
		private readonly List<fluid_voice> freeVoices = new List<fluid_voice>();

		/** Reset the object and set it up to play a new note. */
		public void PlayNote(Tone tone, SoundFont sf, HiPreset instrument, int sampleRate, int duration = 100, int velocity = 127, int delay = 0) {
			StopNote();
			channel.init_ctrl();

			int key = (int) tone;
			int vel = Math.Min(Math.Max(velocity, 0), 127);

			// run through all the zones of this preset
			foreach (HiZone preset_zone in instrument.Zone) {
				// check if the note falls into the key and velocity range of this preset
				if (preset_zone.KeyLo > key || preset_zone.KeyHi < key || preset_zone.VelLo > vel || preset_zone.VelHi < vel) continue;
				if (preset_zone.Index < 0) continue;
				
				HiInstrument inst = sf.HiSf.inst[preset_zone.Index];
				HiZone global_inst_zone = inst.GlobalZone;

				// run through all the zones of this instrument */
				foreach (HiZone inst_zone in inst.Zone) {
					if (inst_zone.Index < 0 || inst_zone.Index >= sf.HiSf.Samples.Length)
						continue;

					// make sure this instrument zone has a valid sample
					HiSample hiSample = sf.HiSf.Samples[inst_zone.Index];
					if (hiSample == null)
						continue;

					// check if the note falls into the key and velocity range of this instrument

					if (inst_zone.KeyLo > key || inst_zone.KeyHi < key || inst_zone.VelLo > vel || inst_zone.VelHi < vel) continue;
					
					// Found a sample to play
					fluid_voice voice;
					int lastFreeVoiceIndex = freeVoices.Count - 1;
					if (lastFreeVoiceIndex >= 0) {
						voice = freeVoices[lastFreeVoiceIndex];
						freeVoices.RemoveAt(lastFreeVoiceIndex);
					} else {
						voice = new fluid_voice();
					}
					activeVoices.Add(voice);
								
					voice.fluid_voice_init(sampleRate, 0, channel, key, vel, hiSample);

					//
					// Instrument level - Generator
					// ----------------------------

					// Global zone

					// SF 2.01 section 9.4 'bullet' 4: A generator in a local instrument zone supersedes a global instrument zone generator.
					// Both cases supersede the default generator. The generator not defined in this instrument do nothing, leave it at the default.

					if (global_inst_zone?.gens != null)
						foreach (HiGen gen in global_inst_zone.gens) {
							//fluid_voice_gen_set(voice, i, global_inst_zone.gen[i].val);
							voice.gens[(int) gen.type].Val = gen.Val;
							voice.gens[(int) gen.type].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;
						}

					// Local zone
					if (inst_zone.gens != null)
						foreach (HiGen gen in inst_zone.gens) {
							//fluid_voice_gen_set(voice, i, global_inst_zone.gen[i].val);
							voice.gens[(int) gen.type].Val = gen.Val;
							voice.gens[(int) gen.type].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;
						}

					//
					// Instrument level - Modulators
					// -----------------------------

					// Global zone
					var mod_list = new List<HiMod>();
					if (global_inst_zone?.mods != null) {
						foreach (HiMod mod in global_inst_zone.mods)
							mod_list.Add(mod);
					}

					// Local zone
					if (inst_zone.mods != null) {
						foreach (HiMod mod in inst_zone.mods) {
							// 'Identical' modulators will be deleted by setting their list entry to NULL.  The list length is known.
							// NULL entries will be ignored later.  SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.

							foreach (HiMod mod1 in mod_list) {
								// fluid_mod_test_identity(mod, mod_list[i]))
								if (mod1.Dest != mod.Dest || mod1.Src1 != mod.Src1 || mod1.Src2 != mod.Src2 || mod1.Flags1 != mod.Flags1 || mod1.Flags2 != mod.Flags2) continue;
								mod1.Amount = mod.Amount;
								break;
							}
						}
					}
					// Add instrument modulators (global / local) to the voice.
					// Instrument modulators -supersede- existing (default) modulators.  SF 2.01 page 69, 'bullet' 6
					foreach (HiMod mod1 in mod_list) {
						voice.fluid_voice_add_mod(mod1, fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE);
					}

					//
					// Preset level - Generators
					// -------------------------

					//  Local zone
					if (preset_zone.gens != null) {
						foreach (HiGen gen in preset_zone.gens) {
							//fluid_voice_gen_incr(voice, i, preset.global_zone.gen[i].val);
							//if (gen.type==fluid_gen_type.GEN_VOLENVATTACK)
							voice.gens[(int) gen.type].Val += gen.Val;
							voice.gens[(int) gen.type].flags = fluid_gen_flags.GEN_SET_PRESET;
						}
					}

					// Global zone
					if (instrument.GlobalZone?.gens != null) {
						foreach (HiGen gen in instrument.GlobalZone.gens) {
							// If not incremented in local, increment in global
							if (voice.gens[(int) gen.type].flags == fluid_gen_flags.GEN_SET_PRESET) continue;
							
							voice.gens[(int) gen.type].Val += gen.Val;
							voice.gens[(int) gen.type].flags = fluid_gen_flags.GEN_SET_PRESET;
						}
					}

					//
					// Preset level - Modulators
					// -------------------------

					// Global zone
					mod_list = new List<HiMod>();
					if (instrument.GlobalZone?.mods != null) {
						foreach (HiMod mod in instrument.GlobalZone.mods) {
							mod_list.Add(mod);
						}
					}

					// Local zone
					if (preset_zone.mods != null) {
						foreach (HiMod mod in preset_zone.mods) {
							// 'Identical' modulators will be deleted by setting their list entry to NULL.  The list length is known.
							// NULL entries will be ignored later.  SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.

							foreach (HiMod mod1 in mod_list) {
								if (mod1.Dest != mod.Dest || mod1.Src1 != mod.Src1 || mod1.Src2 != mod.Src2 || mod1.Flags1 != mod.Flags1 || (mod1.Flags2 != mod.Flags2)) continue;
								
								mod1.Amount = mod.Amount;
								break;
							}
						}
					}

					// Add preset modulators (global / local) to the voice.
					foreach (HiMod mod1 in mod_list)
						if (mod1.Amount != 0d)
							// Preset modulators -add- to existing instrument default modulators.
							// SF2.01 page 70 first bullet on page
							voice.fluid_voice_add_mod(mod1, fluid_voice_addorover_mod.FLUID_VOICE_ADD);

					/* Start the new voice */
					voice.fluid_voice_start(delay, duration);
				}
			}
		}

		public void NoteControl(MPTKController controller, short value) {
			fluid_channel channel = this.channel;
			channel.cc[(int)controller] = value;

			bool modulateIsCC;
			
			switch (controller) {
				case MPTKController.Sustain:
					if (value < 64) {
						foreach (fluid_voice voice in activeVoices) {
							if (voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
								voice.fluid_voice_noteoff(true);
						}
					} // else sustain on //NOTE(jp): ???
					return;

				case MPTKController.BankSelect:
				case MPTKController.BankSelectLsb:
					return;

				case MPTKController.AllNotesOff:
					ReleaseNote();
					return;

				case MPTKController.AllSoundOff:
					StopNote();
					return;

				case MPTKController.ResetAllControllers:
					channel.init_ctrl();
					// Tell all synthesis processes on this channel to update their synthesis parameters after an all control off message (i.e. all controller have been reset to their default value).
					foreach (fluid_voice voice in activeVoices) {
						voice.fluid_voice_modulate_all();
					}
					return;

				case MPTKController.PitchWheel://STRANGE //NOTE(jp): Isn't it?
					channel.pitch_bend = value;
					modulateIsCC = false;
					break;// Modulate after
					
				default:
					modulateIsCC = true;
					break; // Modulate after
			}
			
			// tell all active voices to update their synthesis parameters after a control change.
			foreach (fluid_voice voice in activeVoices) {
				voice.fluid_voice_modulate(modulateIsCC, (int)controller);
			}
		}

		/**
		 * Release the playing note, if any.
		 * <param name="force">True to ignore sustain parameter, false by default.</param>
		 */
		public void ReleaseNote(bool force = false) {
			foreach (var voice in activeVoices) {
				voice.fluid_voice_noteoff(force);
			}
		}
		
		/** Completely and abruptly stop any notes and voices currently running, resetting the whole object. */
		public void StopNote() {
			foreach (var voice in activeVoices) {
				voice.fluid_voice_off();
			}
			freeVoices.AddRange(activeVoices);
			activeVoices.Clear();
		}

		private static readonly float[] NO_SAMPLES = new float[0];
		
		/**
		 * Generate more audio data and return a buffer which contains that data,
		 * in specified sampling frequency, in mono.
		 * Returns null when the note is over.
		 */
		public float[] PullAudio(out int sampleCount) {
			sampleCount = 0;
			if (activeVoices.Count <= 0) {
				return null;
			}
			float[] samples = NO_SAMPLES;
			
			for (var voiceIndex = activeVoices.Count - 1; voiceIndex >= 0; voiceIndex--) {
				var voice = activeVoices[voiceIndex];
				int voiceSampleCount = voice.fluid_voice_write();
				if (voiceSampleCount > 0) {
					float[] sampleBuffer = voice.dsp_buf;
					if (samples == null) {
						samples = sampleBuffer;
						sampleCount = voiceSampleCount;
					} else if (voiceSampleCount <= sampleCount) {
						for (var s = 0; s < voiceSampleCount; s++) {
							samples[s] += sampleBuffer[s];
						}
					} else {
						for (var s = 0; s < sampleCount; s++) {
							sampleBuffer[s] += samples[s];
						}

						samples = sampleBuffer;
						sampleCount = voiceSampleCount;
					}
				}

				if (voice.status != fluid_voice_status.FLUID_VOICE_OFF) {
					continue;
				}

				freeVoices.Add(voice);
				activeVoices.RemoveAt(voiceIndex);
			}

			return samples;
		}
	}
}