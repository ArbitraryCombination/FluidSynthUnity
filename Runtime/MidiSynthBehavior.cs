// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FluidSynth;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FluidSynthUnity {

	/// Class for the Synthesizer. Contains all the function to load a SoundFont, process a MidiEvent,
	/// play voices associated to the Midi Event and play generated notes through PlayEvent().
	/// Duration can be set in the MPTKEvent, but a note can also be stopped with StopEvent().
	public class MidiSynthBehavior : AudioGeneratorBehavior {

		[Range(1, 100)]
		public int DevicePerformance = 40;

		[Range(0, 100)]
		public float MaxDspLoad = 0.6f;

		/// <summary>
		/// If the same note is hit twice on the same channel, then the older voice process is advanced to the release stage. It's the default Midi processing.
		/// </summary>
		public bool MPTK_ReleaseSameNote = false;

		/// <summary>
		/// Find the exclusive class of this voice. If set, kill all voices that match the exclusive class
		/// and are younger than the first voice process created by this noteon event.
		/// </summary>
		public bool MPTK_KillByExclusiveClass = true;

		/** the channels */
		private readonly fluid_channel[] Channels = new fluid_channel[16];

		private readonly List<fluid_voice> ActiveVoices = new List<fluid_voice>();

		/** the synthesis processes */
		private readonly List<fluid_voice> FreeVoices = new List<fluid_voice>();

		/** the synthesis processes */
		private readonly ConcurrentQueue<SynthCommand> QueueSynthCommand = new ConcurrentQueue<SynthCommand>();

		public class SynthCommand {
			public enum enCmd {
				StartEvent,
				StopEvent,
				ClearAllVoices,
				NoteOffAll
			}

			public enCmd Command;
			public MPTKEvent MidiEvent;
		}

		/** Stores a mix of all voices. */
		private readonly float[] synthBuffer = new float[fluid_voice.FLUID_BUFSIZE];
		private int synthBufferSampleCount = 0;
		private int synthBufferSamplePosition = 0;

		protected override void InitializeAudioGenerator() {
			MPTK_InitSynth();
			QueueSynthCommand.Enqueue(new SynthCommand {Command = SynthCommand.enCmd.ClearAllVoices});
		}

		public MPTKEvent PlayNote(Tone tone, (int bank, int num) instrument, int duration = 100, int velocity = 127, int delay = 0, int channel = 0) {
			var sf = SoundFontManager.soundFont;

			if (sf == null || !SoundFontManager.soundFontInitialized) {
				// Not loaded yet
				return null;
			}

			MPTKEvent note = new MPTKEvent {
				Command = MPTKCommand.NoteOn,
				Duration = duration,
				Velocity = velocity,
				Value = (int) tone,
				Instrument = sf.Instruments[instrument],
				Channel = channel,
				DelayMs = delay
			};
			PlayEvent(note);
			return note;
		}

		/// Play one midi event with a thread so the call return immediately.
		public void PlayEvent(MPTKEvent evnt) {
			QueueSynthCommand.Enqueue(new SynthCommand {Command = SynthCommand.enCmd.StartEvent, MidiEvent = evnt});
		}

		/// Stop playing the note. All waves associated to the note are stop by sending a noteoff.
		public void StopEvent(MPTKEvent pnote) {
			QueueSynthCommand.Enqueue(new SynthCommand {Command = SynthCommand.enCmd.StopEvent, MidiEvent = pnote});
		}

		protected override void GenerateAudio(float[] data, int channels, long ticks) {
			ProcessQueueCommand();
			MoveVoiceToFree();

			int dataLength = data.Length / channels;
			int dataPosition = 0;
			while (dataPosition < dataLength) {
				int synthSamples = synthBufferSampleCount - synthBufferSamplePosition;
				if (synthSamples <= 0) {
					// Generate new synth samples
					Array.Clear(synthBuffer, 0, fluid_voice.FLUID_BUFSIZE);

					foreach (fluid_voice voice in ActiveVoices) {
						//NOTE(jp): This is not precise delay handling, but it will have to do
						var delaySamples = voice.fluid_voice_consume_delay(synthBuffer.Length);
						if (delaySamples > synthBuffer.Length / 2) {
							continue;
						}

						int sampleCount = voice.fluid_voice_write();
						float[] sampleBuffer = voice.dsp_buf;
						for (int i = 0; i < sampleCount; i++) {
							synthBuffer[i] += sampleBuffer[i];
						}
					}

					synthBufferSampleCount = fluid_voice.FLUID_BUFSIZE;
					synthBufferSamplePosition = 0;
					synthSamples = fluid_voice.FLUID_BUFSIZE;
				}
				int copySamplesCount = Math.Min(synthSamples, dataLength - dataPosition);

				int start = dataPosition * channels;
				int end = start + copySamplesCount * channels;
				int synthStart = synthBufferSamplePosition;
				for (var c = 0; c < channels; c++) {
					int synthI = synthStart;
					for (int i = start + c; i < end; i += channels) {
						data[i] *= synthBuffer[synthI++];
					}
				}

				dataPosition += copySamplesCount;
				synthBufferSamplePosition += copySamplesCount;
			}
		}

		protected override void OnSampleRateChanged(int newSampleRate) {
			foreach (fluid_voice v in ActiveVoices)
				v.output_rate = newSampleRate;
		}

		/// Initialize the synthetizer: channel, voices, modulator. It's not useful to call this method if you are using prefabs (MidiFilePlayer, MidiStreamPlayer, ...).
		/// Each gameObjects created from these prefabs have their own, autonomous and isolated synth.
		private void MPTK_InitSynth() {
			for (int i = 0; i < Channels.Length; i++) {
				(Channels[i] = new fluid_channel()).init_ctrl();
			}
		}

		/// Allocate a synthesis voice. This function is called by a soundfont's preset in response to a noteon event.
		/// The returned voice comes with default modulators installed(velocity-to-attenuation, velocity to filter, ...)
		/// Note: A single noteon event may create any number of voices, when the preset is layered. Typically 1 (mono) or 2 (stereo).
		private fluid_voice fluid_synth_alloc_voice(HiSample hiSample, int chan, int key, int vel) {

			/*   fluid_mutex_lock(synth.busy); /\* Don't interfere with the audio thread *\/ */
			/*   fluid_mutex_unlock(synth.busy); */

			// check if there's an available free voice
			fluid_voice voice;
			int lastFreeVoiceIndex = FreeVoices.Count - 1;
			if (lastFreeVoiceIndex >= 0) {
				voice = FreeVoices[lastFreeVoiceIndex];
				FreeVoices.RemoveAt(lastFreeVoiceIndex);
			} else {
				// No found existing voice, instanciate a new one
				voice = new fluid_voice();
			}

			if (chan < 0 || chan >= Channels.Length) {
				Debug.LogFormat("Channel out of range chan:{0}", chan);
				chan = 0;
			}

			// Defined default voice value. Called also when a voice is reused.
			var sample = SoundFontManager.soundFontSamples[hiSample.Name];
			if (sample == null) {
				Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} data not loaded", hiSample.Name);
				return null;
			}
			voice.fluid_voice_init(OutputRate, chan, Channels[chan], key, vel, sample);

			ActiveVoices.Add(voice);

			return voice;
		}

		private void fluid_synth_kill_by_exclusive_class(fluid_voice new_voice, int activeVoiceCount) {
			//fluid_synth_t* synth
			/* Kill all voices on a given channel, which belong into
			    excl_class.  This function is called by a SoundFont's preset in
			    response to a noteon event.  If one noteon event results in
			    several voice processes (stereo samples), ignore_ID must name
			    the voice ID of the first generated voice (so that it is not
			    stopped). The first voice uses ignore_ID=-1, which will
			    terminate all voices on a channel belonging into the exclusive
			    class excl_class.
			*/

			int excl_class = (int) new_voice.gens[(int) fluid_gen_type.GEN_EXCLUSIVECLASS].Val;
			/* Check if the voice belongs to an exclusive class. In that case, previous notes from the same class are released. */

			/* Excl. class 0: No exclusive class */
			if (excl_class == 0) {
				return;
			}

			/* Kill all notes on the same channel with the same exclusive class */

			// NOTE(jp): We don't want to accidentaly turn off a voice that we just added as a part of this note
			// (for example for stereo samples). So we don't inspect the whole ActiveVoices list, but only whatever
			// was there from older previous notes.
			for (int i = 0; i < activeVoiceCount; i++) {
				fluid_voice voice = ActiveVoices[i];

				/* Existing voice does not play? Leave it alone. */
				if (voice.status != fluid_voice_status.FLUID_VOICE_ON ||
				    voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED) {
					continue;
				}

				/* An exclusive class is valid for a whole channel (or preset). Is the voice on a different channel? Leave it alone. */
				if (voice.chan != new_voice.chan) {
					continue;
				}

				/* Existing voice has a different (or no) exclusive class? Leave it alone. */
				if ((int) voice.gens[(int) fluid_gen_type.GEN_EXCLUSIVECLASS].Val != excl_class) {
					continue;
				}

				voice.fluid_voice_kill_excl();
			}
		}

		/// Start a synthesis voice. This function is called by a soundfont's preset in response to a noteon event after the voice  has been allocated with fluid_synth_alloc_voice() and initialized.
		/// Exclusive classes are processed here.
		private void synth_noteon(MPTKEvent note) {
			int key = note.Value;
			int vel = note.Velocity;

			HiPreset preset = note.Instrument;

			// If the same note is hit twice on the same channel, then the older voice process is advanced to the release stage.
			if (MPTK_ReleaseSameNote)
				fluid_synth_release_voice_on_same_note(note.Channel, key);

			int activeVoiceCount = ActiveVoices.Count;

			SoundFont sfont = SoundFontManager.soundFont;
			note.Voices = new List<fluid_voice>();

			// run thru all the zones of this preset
			foreach (HiZone preset_zone in preset.Zone) {
				// check if the note falls into the key and velocity range of this preset
				if ((preset_zone.KeyLo <= key) &&
				    (preset_zone.KeyHi >= key) &&
				    (preset_zone.VelLo <= vel) &&
				    (preset_zone.VelHi >= vel)) {
					if (preset_zone.Index >= 0) {
						HiInstrument inst = sfont.HiSf.inst[preset_zone.Index];
						HiZone global_inst_zone = inst.GlobalZone;

						// run thru all the zones of this instrument */
						foreach (HiZone inst_zone in inst.Zone) {
							if (inst_zone.Index < 0 || inst_zone.Index >= sfont.HiSf.Samples.Length)
								continue;

							// make sure this instrument zone has a valid sample
							HiSample hiSample = sfont.HiSf.Samples[inst_zone.Index];
							if (hiSample == null)
								continue;

							// check if the note falls into the key and velocity range of this instrument

							if (inst_zone.KeyLo <= key &&
							    inst_zone.KeyHi >= key &&
							    inst_zone.VelLo <= vel &&
							    inst_zone.VelHi >= vel) {
								//
								// Found a sample to play
								fluid_voice voice = fluid_synth_alloc_voice(hiSample, note.Channel, key, vel);

								if (voice == null) return;

								note.Voices.Add(voice);

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
								if (inst_zone.mods != null)
									foreach (HiMod mod in inst_zone.mods) {
										// 'Identical' modulators will be deleted by setting their list entry to NULL.  The list length is known.
										// NULL entries will be ignored later.  SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.

										foreach (HiMod mod1 in mod_list) {
											// fluid_mod_test_identity(mod, mod_list[i]))
											if ((mod1.Dest == mod.Dest) &&
											    (mod1.Src1 == mod.Src1) &&
											    (mod1.Src2 == mod.Src2) &&
											    (mod1.Flags1 == mod.Flags1) &&
											    (mod1.Flags2 == mod.Flags2)) {
												mod1.Amount = mod.Amount;
												break;
											}
										}
									}
								// Add instrument modulators (global / local) to the voice.
								// Instrument modulators -supersede- existing (default) modulators.  SF 2.01 page 69, 'bullet' 6
								foreach (HiMod mod1 in mod_list)
									voice.fluid_voice_add_mod(mod1, fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE);

								//
								// Preset level - Generators
								// -------------------------

								//  Local zone
								if (preset_zone.gens != null)
									foreach (HiGen gen in preset_zone.gens) {
										//fluid_voice_gen_incr(voice, i, preset.global_zone.gen[i].val);
										//if (gen.type==fluid_gen_type.GEN_VOLENVATTACK)
										voice.gens[(int) gen.type].Val += gen.Val;
										voice.gens[(int) gen.type].flags = fluid_gen_flags.GEN_SET_PRESET;
									}

								// Global zone
								if (preset.GlobalZone?.gens != null) {
									foreach (HiGen gen in preset.GlobalZone.gens) {
										// If not incremented in local, increment in global
										if (voice.gens[(int) gen.type].flags != fluid_gen_flags.GEN_SET_PRESET) {
											//fluid_voice_gen_incr(voice, i, preset.global_zone.gen[i].val);
											voice.gens[(int) gen.type].Val += gen.Val;
											voice.gens[(int) gen.type].flags = fluid_gen_flags.GEN_SET_PRESET;
										}
									}
								}

								//
								// Preset level - Modulators
								// -------------------------

								// Global zone
								mod_list = new List<HiMod>();
								if (preset.GlobalZone?.mods != null) {
									foreach (HiMod mod in preset.GlobalZone.mods)
										mod_list.Add(mod);
								}

								// Local zone
								if (preset_zone.mods != null)
									foreach (HiMod mod in preset_zone.mods) {
										// 'Identical' modulators will be deleted by setting their list entry to NULL.  The list length is known.
										// NULL entries will be ignored later.  SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.

										foreach (HiMod mod1 in mod_list) {
											// fluid_mod_test_identity(mod, mod_list[i]))
											if ((mod1.Dest == mod.Dest) &&
											    (mod1.Src1 == mod.Src1) &&
											    (mod1.Src2 == mod.Src2) &&
											    (mod1.Flags1 == mod.Flags1) &&
											    (mod1.Flags2 == mod.Flags2)) {
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

								// Find the exclusive class of this voice. If set, kill all voices that match the exclusive class
								// and are younger than the first voice process created by this noteon event.
								if (MPTK_KillByExclusiveClass)
									fluid_synth_kill_by_exclusive_class(voice, activeVoiceCount);

								/* Start the new voice */
								voice.fluid_voice_start(note.DelayMs, note.Duration);

								/* Store the ID of the first voice that was created by this noteon event.
								 * Exclusive class may only terminate older voices.
								 * That avoids killing voices, which have just been created.
								 * (a noteon event can create several voice processes with the same exclusive
								 * class - for example when using stereo samples)
								 */
							}
						}
					}
				}
			}
		}

		// If the same note is hit twice on the same channel, then the older voice process is advanced to the release stage.
		// Using a mechanical MIDI controller, the only way this can happen is when the sustain pedal is held.
		// In this case the behaviour implemented here is natural for many instruments.
		// Note: One noteon event can trigger several voice processes, for example a stereo sample.  Don't release those...
		private void fluid_synth_release_voice_on_same_note(int chan, int key) {
			foreach (fluid_voice voice in ActiveVoices) {
				if (voice.chan == chan && voice.key == key) {
					voice.fluid_voice_noteoff(true);
					// can't break, beacause need to search in case of multi sample
				}
			}
		}

		private void fluid_synth_noteoff(int pchan, int pkey) {
			foreach (fluid_voice voice in ActiveVoices) {
				// A voice is 'ON', if it has not yet received a noteoff event. Sending a noteoff event will advance the envelopes to  section 5 (release).
				//#define _ON(voice)  ((voice)->status == FLUID_VOICE_ON && (voice)->volenv_section < FLUID_VOICE_ENVRELEASE)
				if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
				    voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
				    voice.chan == pchan &&
				    (pkey == -1 || voice.key == pkey)) {
					voice.fluid_voice_noteoff();
				}
			}
		}

		private void fluid_synth_soundoff(int pchan) {
			foreach (fluid_voice voice in ActiveVoices) {
				// A voice is 'ON', if it has not yet received a noteoff event. Sending a noteoff event will advance the envelopes to  section 5 (release).
				//#define _ON(voice)  ((voice)->status == FLUID_VOICE_ON && (voice)->volenv_section < FLUID_VOICE_ENVRELEASE)
				if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
				    voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
				    voice.chan == pchan) {
					voice.fluid_voice_off();
				}
			}
		}

		private void fluid_synth_damp_voices(int pchan) {
			foreach (fluid_voice voice in ActiveVoices) {
				if (voice.chan == pchan && voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
					voice.fluid_voice_noteoff(true);
			}
		}

		/// tell all synthesis activ voices on this channel to update their synthesis parameters after a control change.
		private void fluid_synth_modulate_voices(int chan, bool is_cc, int ctrl) {
			foreach (fluid_voice voice in ActiveVoices) {
				if (voice.chan == chan && voice.status != fluid_voice_status.FLUID_VOICE_OFF)
					voice.fluid_voice_modulate(is_cc, ctrl);
			}
		}

		/// Tell all synthesis processes on this channel to update their synthesis parameters after an all control off message (i.e. all controller have been reset to their default value).
		private void fluid_synth_modulate_voices_all(int chan) {
			foreach (fluid_voice voice in ActiveVoices) {
				if (voice.chan == chan)
					voice.fluid_voice_modulate_all();
			}
		}

		private void fluid_synth_pitch_bend(int chan, int val) {
			/*   fluid_mutex_lock(busy); /\* Don't interfere with the audio thread *\/ */
			/*   fluid_mutex_unlock(busy); */

			/* check the ranges of the arguments */
			if (chan < 0 || chan >= Channels.Length) {
				Debug.LogFormat("Channel out of range chan:{0}", chan);
				return;
			}

			/* set the pitch-bend value in the channel */
			fluid_channel_pitch_bend(chan, val);
		}

		private void ProcessQueueCommand() {
			try {
				while (QueueSynthCommand.TryDequeue(out var action)) {
					var midievent = action.MidiEvent;
					switch (action.Command) {
						case SynthCommand.enCmd.StartEvent:
							switch (midievent.Command) {
								case MPTKCommand.NoteOn:
									if (midievent.Velocity != 0) {
										synth_noteon(midievent);
									} else {
										fluid_synth_noteoff(midievent.Channel, midievent.Value);
									}
									break;
								case MPTKCommand.NoteOff:
									fluid_synth_noteoff(midievent.Channel, midievent.Value);
									break;
								case MPTKCommand.ControlChange:
									fluid_channel_cc(midievent.Channel, midievent.Controller, midievent.Value);
									break;
								case MPTKCommand.PitchWheelChange:
									fluid_synth_pitch_bend(midievent.Channel, midievent.Value);
									break;
							}
							break;
						case SynthCommand.enCmd.StopEvent:
							try {
								if (midievent?.Voices != null) {
									foreach (fluid_voice voice in midievent.Voices) {
										if (voice.volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
										    voice.status != fluid_voice_status.FLUID_VOICE_OFF)
											voice.fluid_voice_noteoff();
									}
								}
							} catch (Exception ex) {
								Debug.LogException(ex);
							}
							break;
						case SynthCommand.enCmd.ClearAllVoices:
							ActiveVoices.Clear();
							break;
						case SynthCommand.enCmd.NoteOffAll:
							foreach (fluid_voice voice in ActiveVoices) {
								if ((voice.status == fluid_voice_status.FLUID_VOICE_ON ||
								     voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)) {
									voice.fluid_voice_noteoff(true);
								}
							}
							break;
					}
				}
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
		}

		private void MoveVoiceToFree() {
			bool firstToKill = false;
			for (int indexVoice = 0; indexVoice < ActiveVoices.Count;) {
				fluid_voice voice = ActiveVoices[indexVoice];
				try {
					if (AudioProcessingLoad > MaxDspLoad) {
						// Check if there is voice which are sustained: Midi message ControlChange with Sustain (64)
						if (voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED) {
							voice.fluid_voice_noteoff(true);
						}

						if (voice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE) {
							// reduce release time
							float count = voice.volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count;
							count *= DevicePerformance / 100f;
							voice.volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count = (uint) count;
						}

						if (!firstToKill && DevicePerformance <= 25) // V2.82 Try to stop one older voice (the first in the list of active voice)
						{
							if (voice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD ||
							    voice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN) {
								firstToKill = true;
								voice.fluid_voice_noteoff(true);
							}
						}
					}

					if (voice.status == fluid_voice_status.FLUID_VOICE_OFF) {
						ActiveVoices.RemoveAt(indexVoice);
						if (FreeVoices.Count < 1000) {
							FreeVoices.Add(voice);
						}
					} else {
						indexVoice++;
					}
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
		}

		private void fluid_channel_cc(int channum, MPTKController numController, int valueController) {
	        fluid_channel channel = Channels[channum];
	        channel.cc[(int)numController] = (short)valueController;

            switch (numController) {
                case MPTKController.Sustain:
                    if (valueController < 64) {
                        /*  	printf("** sustain off\n"); */
                        fluid_synth_damp_voices(channum);
                    } // else sustain on //NOTE(jp): ???
                    break;

                case MPTKController.BankSelect:
                    break;

                case MPTKController.BankSelectLsb:
                    {
                        // Not implemented
                        // FIXME: according to the Downloadable Sounds II specification, bit 31 should be set when we receive the message on channel 10 (drum channel)
                        //TBC fluid_channel_set_banknum(chan, (((unsigned int)value & 0x7f) + ((unsigned int)chan->bank_msb << 7)));
                    }
                    break;

                case MPTKController.AllNotesOff:
                    fluid_synth_noteoff(channum, -1);
                    break;

                case MPTKController.AllSoundOff:
                    fluid_synth_soundoff(channum);
                    break;

                case MPTKController.ResetAllControllers:
	                channel.init_ctrl();
                    fluid_synth_modulate_voices_all(channum);
                    break;

                default:
                    fluid_synth_modulate_voices(channum, true, (int)numController);
                    break;
            }
        }

        private void fluid_channel_pitch_bend(int channum, int val) {
	        fluid_channel channel = Channels[channum];
	        channel.pitch_bend = (short)val;
            fluid_synth_modulate_voices(channum, false, (int)fluid_mod_src.FLUID_MOD_PITCHWHEEL); //STRANGE //NOTE(jp): Isn't it?
        }
	}

}
