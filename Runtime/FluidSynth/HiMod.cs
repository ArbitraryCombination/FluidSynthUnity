namespace FluidSynth {

	/* Flags telling the polarity of a modulator.  Compare with SF2.01
	   section 8.2. Note: The numbers of the bits are different!  (for
	   example: in the flags of a SF modulator, the polarity bit is bit
	   nr. 9) */
	public enum fluid_mod_flags {
		FLUID_MOD_POSITIVE = 0,
		FLUID_MOD_NEGATIVE = 1,
		FLUID_MOD_UNIPOLAR = 0,
		FLUID_MOD_BIPOLAR = 2,
		FLUID_MOD_LINEAR = 0,
		FLUID_MOD_CONCAVE = 4,
		FLUID_MOD_CONVEX = 8,
		FLUID_MOD_SWITCH = 12,
		FLUID_MOD_GC = 0,
		FLUID_MOD_CC = 16
	}

	/* Flags telling the source of a modulator.  This corresponds to
	 * SF2.01 section 8.2.1 */
	public enum fluid_mod_src {
		FLUID_MOD_NONE = 0,
		FLUID_MOD_VELOCITY = 2,
		FLUID_MOD_KEY = 3,
		FLUID_MOD_KEYPRESSURE = 10,
		FLUID_MOD_CHANNELPRESSURE = 13,
		FLUID_MOD_PITCHWHEEL = 14,
		FLUID_MOD_PITCHWHEELSENS = 16
	}

	/// <summary>
	/// Defined Modulator from fluid_mod_t
	/// </summary>
	public class HiMod {
		/* Maximum number of modulators in a voice */
		public const int FLUID_NUM_MOD = 64;

		public byte Dest;
		public byte Src1;
		public byte Flags1;
		public byte Src2;
		public byte Flags2;
		public float Amount;

		// Modulator structure read from SF
		public ushort SfSrc; /* source modulator */
		public ushort SfAmtSrc; /* second source controls amnt of first */
		public ushort SfTrans; /* transform applied to source */

		public float fluid_mod_get_value(fluid_channel chan, int key, int vel) {
			float v1, v2 = 1.0f;
			float range1 = 127.0f, range2 = 127.0f;

			if (chan == null) {
				return 0.0f;
			}

			/* 'special treatment' for default controller
			 *
			 *  Reference: SF2.01 section 8.4.2
			 *
			 * The GM default controller 'vel-to-filter cut off' is not clearly defined: If implemented according to the specs, the filter
			 * frequency jumps between vel=63 and vel=64.  To maintain compatibility with existing sound fonts, the implementation is
			 * 'hardcoded', it is impossible to implement using only one modulator otherwise.
			 *
			 * I assume here, that the 'intention' of the paragraph is one octave (1200 cents) filter frequency shift between vel=127 and
			 * vel=64.  'amount' is (-2400), at least as long as the controller is set to default.
			 *
			 * Further, the 'appearance' of the modulator (source enumerator, destination enumerator, flags etc) is different from that
			 * described in section 8.4.2, but it matches the definition used in several SF2.1 sound fonts (where it is used only to turn it off).
			 * */
			if ((Dest == (byte) fluid_gen_type.GEN_FILTERFC) &&
			    (Src2 == (int) fluid_mod_src.FLUID_MOD_VELOCITY) &&
			    (Src1 == (int) fluid_mod_src.FLUID_MOD_VELOCITY) &&
			    (Flags1 == ((byte) fluid_mod_flags.FLUID_MOD_GC | (byte) fluid_mod_flags.FLUID_MOD_UNIPOLAR |
			                (byte) fluid_mod_flags.FLUID_MOD_NEGATIVE | (byte) fluid_mod_flags.FLUID_MOD_LINEAR)) &&
			    (Flags2 == ((byte) fluid_mod_flags.FLUID_MOD_GC | (byte) fluid_mod_flags.FLUID_MOD_UNIPOLAR |
			                (byte) fluid_mod_flags.FLUID_MOD_POSITIVE | (byte) fluid_mod_flags.FLUID_MOD_SWITCH))) {
				if (vel < 64) {
					return Amount / 2.0f;
				} else {
					return Amount * (127f - vel) / 127f;
				}
			}

			/* get the initial value of the first source */
			if (Src1 > 0) {
				if ((Flags1 & (byte) fluid_mod_flags.FLUID_MOD_CC) > 0) {
					v1 = Src1 < 128 ? chan.cc[Src1] : 0;
				} else {
					/* source 1 is one of the direct controllers */
					switch (Src1) {
						case (int) fluid_mod_src.FLUID_MOD_NONE: /* SF 2.01 8.2.1 item 0: src enum=0 => value is 1 */
							v1 = range1;
							break;
						case (int) fluid_mod_src.FLUID_MOD_VELOCITY:
							v1 = vel;
							break;
						case (int) fluid_mod_src.FLUID_MOD_KEY:
							v1 = key;
							break;
						case (int) fluid_mod_src.FLUID_MOD_KEYPRESSURE:
							v1 = chan.key_pressure;
							break;
						case (int) fluid_mod_src.FLUID_MOD_CHANNELPRESSURE:
							v1 = chan.channel_pressure;
							break;
						case (int) fluid_mod_src.FLUID_MOD_PITCHWHEEL:
							v1 = chan.pitch_bend;
							range1 = 0x4000;
							break;
						case (int) fluid_mod_src.FLUID_MOD_PITCHWHEELSENS:
							v1 = chan.pitch_wheel_sensitivity;
							break;
						default:
							v1 = 0.0f;
							break;
					}
				}

				/* transform the input value */
				switch (Flags1 & 0x0f) {
					case 0: /* linear, unipolar, positive */
						v1 /= range1;
						break;
					case 1: /* linear, unipolar, negative */
						v1 = 1.0f - v1 / range1;
						break;
					case 2: /* linear, bipolar, positive */
						v1 = -1.0f + 2.0f * v1 / range1;
						break;
					case 3: /* linear, bipolar, negative */
						v1 = -1.0f + 2.0f * v1 / range1;
						break;
					case 4: /* concave, unipolar, positive */
						v1 = fluid_conv.fluid_concave(v1);
						break;
					case 5: /* concave, unipolar, negative */
						v1 = fluid_conv.fluid_concave(127 - v1);
						break;
					case 6: /* concave, bipolar, positive */
						v1 = (v1 > 64)
							? fluid_conv.fluid_concave(2 * (v1 - 64))
							: -fluid_conv.fluid_concave(2 * (64 - v1));
						break;
					case 7: /* concave, bipolar, negative */
						v1 = (v1 > 64)
							? -fluid_conv.fluid_concave(2 * (v1 - 64))
							: fluid_conv.fluid_concave(2 * (64 - v1));
						break;
					case 8: /* convex, unipolar, positive */
						v1 = fluid_conv.fluid_convex(v1);
						break;
					case 9: /* convex, unipolar, negative */
						v1 = fluid_conv.fluid_convex(127 - v1);
						break;
					case 10: /* convex, bipolar, positive */
						v1 = (v1 > 64)
							? -fluid_conv.fluid_convex(2 * (v1 - 64))
							: fluid_conv.fluid_convex(2 * (64 - v1));
						break;
					case 11: /* convex, bipolar, negative */
						v1 = (v1 > 64)
							? -fluid_conv.fluid_convex(2 * (v1 - 64))
							: fluid_conv.fluid_convex(2 * (64 - v1));
						break;
					case 12: /* switch, unipolar, positive */
						v1 = (v1 >= 64) ? 1.0f : 0.0f;
						break;
					case 13: /* switch, unipolar, negative */
						v1 = (v1 >= 64) ? 0.0f : 1.0f;
						break;
					case 14: /* switch, bipolar, positive */
						v1 = (v1 >= 64) ? 1.0f : -1.0f;
						break;
					case 15: /* switch, bipolar, negative */
						v1 = (v1 >= 64) ? -1.0f : 1.0f;
						break;
				}
			} else {
				return 0.0f;
			}

			/* no need to go further */
			if (v1 == 0.0f) {
				return 0.0f;
			}

			/* get the second input source */
			if (Src2 > 0) {
				if ((Flags2 & (byte) fluid_mod_flags.FLUID_MOD_CC) > 0) {
					v2 = Src2 < 128 ? chan.cc[Src2] : 0;
				} else {
					switch (Src2) {
						case (int) fluid_mod_src.FLUID_MOD_NONE: /* SF 2.01 8.2.1 item 0: src enum=0 => value is 1 */
							v2 = range2;
							break;
						case (int) fluid_mod_src.FLUID_MOD_VELOCITY:
							v2 = vel;
							break;
						case (int) fluid_mod_src.FLUID_MOD_KEY:
							v2 = key;
							break;
						case (int) fluid_mod_src.FLUID_MOD_KEYPRESSURE:
							v2 = chan.key_pressure;
							break;
						case (int) fluid_mod_src.FLUID_MOD_CHANNELPRESSURE:
							v2 = chan.channel_pressure;
							break;
						case (int) fluid_mod_src.FLUID_MOD_PITCHWHEEL:
							v2 = chan.pitch_bend;
							break;
						case (int) fluid_mod_src.FLUID_MOD_PITCHWHEELSENS:
							v2 = chan.pitch_wheel_sensitivity;
							break;
						default:
							v1 = 0.0f;
							break;
					}
				}

				/* transform the second input value */
				switch (Flags2 & 0x0f) {
					case 0: /* linear, unipolar, positive */
						v2 /= range2;
						break;
					case 1: /* linear, unipolar, negative */
						v2 = 1.0f - v2 / range2;
						break;
					case 2: /* linear, bipolar, positive */
						v2 = -1.0f + 2.0f * v2 / range2;
						break;
					case 3: /* linear, bipolar, negative */
						v2 = -1.0f + 2.0f * v2 / range2;
						break;
					case 4: /* concave, unipolar, positive */
						v2 = fluid_conv.fluid_concave(v2);
						break;
					case 5: /* concave, unipolar, negative */
						v2 = fluid_conv.fluid_concave(127 - v2);
						break;
					case 6: /* concave, bipolar, positive */
						v2 = (v2 > 64)
							? fluid_conv.fluid_concave(2 * (v2 - 64))
							: -fluid_conv.fluid_concave(2 * (64 - v2));
						break;
					case 7: /* concave, bipolar, negative */
						v2 = (v2 > 64)
							? -fluid_conv.fluid_concave(2 * (v2 - 64))
							: fluid_conv.fluid_concave(2 * (64 - v2));
						break;
					case 8: /* convex, unipolar, positive */
						v2 = fluid_conv.fluid_convex(v2);
						break;
					case 9: /* convex, unipolar, negative */
						v2 = 1.0f - fluid_conv.fluid_convex(v2);
						break;
					case 10: /* convex, bipolar, positive */
						v2 = (v2 > 64)
							? -fluid_conv.fluid_convex(2 * (v2 - 64))
							: fluid_conv.fluid_convex(2 * (64 - v2));
						break;
					case 11: /* convex, bipolar, negative */
						v2 = (v2 > 64)
							? -fluid_conv.fluid_convex(2 * (v2 - 64))
							: fluid_conv.fluid_convex(2 * (64 - v2));
						break;
					case 12: /* switch, unipolar, positive */
						v2 = (v2 >= 64) ? 1.0f : 0.0f;
						break;
					case 13: /* switch, unipolar, negative */
						v2 = (v2 >= 64) ? 0.0f : 1.0f;
						break;
					case 14: /* switch, bipolar, positive */
						v2 = (v2 >= 64) ? 1.0f : -1.0f;
						break;
					case 15: /* switch, bipolar, negative */
						v2 = (v2 >= 64) ? -1.0f : 1.0f;
						break;
				}
			} else {
				v2 = 1.0f;
			}

			/* it's as simple as that: */
			return Amount * v1 * v2;
		}

		public override string ToString() {
			return $"Mod amount:{this.Amount} src1:{this.Src1} flags1:{this.Flags1} src2:{this.Src2} flags2:{this.Flags2} dest:{(fluid_gen_type) this.Dest}";
		}

	}

	public static class HiModDefault {
		//default modulators SF2.01 page 52 ff:
		//There is a set of predefined default modulators. They have to be explicitly overridden by the sound font in order to turn them off.
		private static readonly HiMod default_vel2att_mod = new HiMod(); /* SF2.01 section 8.4.1  */
		private static readonly HiMod default_vel2filter_mod = new HiMod(); /* SF2.01 section 8.4.2  */
		private static readonly HiMod default_at2viblfo_mod = new HiMod(); /* SF2.01 section 8.4.3  */
		private static readonly HiMod default_mod2viblfo_mod = new HiMod(); /* SF2.01 section 8.4.4  */
		private static readonly HiMod default_att_mod = new HiMod(); /* SF2.01 section 8.4.5  */
		private static readonly HiMod default_pan_mod = new HiMod(); /* SF2.01 section 8.4.6  */
		private static readonly HiMod default_expr_mod = new HiMod(); /* SF2.01 section 8.4.7  */
		private static readonly HiMod default_reverb_mod = new HiMod(); /* SF2.01 section 8.4.8  */
		private static readonly HiMod default_chorus_mod = new HiMod(); /* SF2.01 section 8.4.9  */
		private static readonly HiMod default_pitch_bend_mod = new HiMod(); /* SF2.01 section 8.4.10 */

		static HiModDefault() {
			/* SF2.01 page 53 section 8.4.1: MIDI Note-On Velocity to Initial Attenuation */
			default_vel2att_mod.Src1 = (int) fluid_mod_src.FLUID_MOD_VELOCITY;
			default_vel2att_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_GC /* Not a MIDI continuous controller */
			                             | (int) fluid_mod_flags.FLUID_MOD_CONCAVE /* Curve shape. Corresponds to 'type=1' */
			                             | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* Polarity. Corresponds to 'P=0' */
			                             | (int) fluid_mod_flags.FLUID_MOD_NEGATIVE;
			default_vel2att_mod.Src2 = 0;
			default_vel2att_mod.Flags2 = 0;
			default_vel2att_mod.Dest = (int) fluid_gen_type.GEN_ATTENUATION; /* Target: Initial attenuation */
			default_vel2att_mod.Amount = 960.0f; /* Modulation amount: 960 */

			/* SF2.01 page 53 section 8.4.2: MIDI Note-On Velocity to Filter Cutoff
			 * Have to make a design decision here. The specs don't make any sense this way or another.
			 * One sound font, 'Kingston Piano', which has been praised for its quality, tries to
			 * override this modulator with an amount of 0 and positive polarity (instead of what
			 * the specs say, D=1) for the secondary source.
			 * So if we change the polarity to 'positive', one of the best free sound fonts works...
			 */
			default_vel2filter_mod.Src1 = (int) fluid_mod_src.FLUID_MOD_VELOCITY;
			default_vel2filter_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_GC /* CC=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_NEGATIVE;
			default_vel2filter_mod.Src2 = (int) fluid_mod_src.FLUID_MOD_VELOCITY;
			default_vel2filter_mod.Flags2 = (int) fluid_mod_flags.FLUID_MOD_GC /* CC=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_SWITCH /* type=3 */
			                                | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                                // do not remove       | FLUID_MOD_NEGATIVE                         /* D=1 */
			                                | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_vel2filter_mod.Dest = (int) fluid_gen_type.GEN_FILTERFC; /* Target: Initial filter cutoff */
			default_vel2filter_mod.Amount = -2400;

			/* SF2.01 page 53 section 8.4.3: MIDI Channel pressure to Vibrato LFO pitch depth */
			default_at2viblfo_mod.Src1 = (int) fluid_mod_src.FLUID_MOD_CHANNELPRESSURE;
			default_at2viblfo_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_GC /* CC=0 */
			                               | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                               | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                               | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_at2viblfo_mod.Src2 = 0;
			default_at2viblfo_mod.Flags2 = 0;
			default_at2viblfo_mod.Dest = (int) fluid_gen_type.GEN_VIBLFOTOPITCH; /* Target: Vib. LFO => pitch */
			default_at2viblfo_mod.Amount = 50;

			/* SF2.01 page 53 section 8.4.4: Mod wheel (Controller 1) to Vibrato LFO pitch depth */
			default_mod2viblfo_mod.Src1 = 1;
			default_mod2viblfo_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_CC /* CC=1 */
			                                | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_mod2viblfo_mod.Src2 = 0;
			default_mod2viblfo_mod.Flags2 = 0;
			default_mod2viblfo_mod.Dest = (int) fluid_gen_type.GEN_VIBLFOTOPITCH; /* Target: Vib. LFO => pitch */
			default_mod2viblfo_mod.Amount = 50;

			/* SF2.01 page 55 section 8.4.5: MIDI continuous controller 7 to initial attenuation*/
			default_att_mod.Src1 = 7;
			default_att_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_CC /* CC=1 */
			                         | (int) fluid_mod_flags.FLUID_MOD_CONCAVE /* type=1 */
			                         | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                         | (int) fluid_mod_flags.FLUID_MOD_NEGATIVE;
			default_att_mod.Src2 = 0;
			default_att_mod.Flags2 = 0;
			default_att_mod.Dest = (int) fluid_gen_type.GEN_ATTENUATION; /* Target: Initial attenuation */
			default_att_mod.Amount = 960.0f; /* Amount: 960 */

			/* SF2.01 page 55 section 8.4.6 MIDI continuous controller 10 to Pan Position */
			default_pan_mod.Src1 = 10;
			default_pan_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_CC /* CC=1 */
			                         | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                         | (int) fluid_mod_flags.FLUID_MOD_BIPOLAR /* P=1 */
			                         | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_pan_mod.Src2 = 0;
			default_pan_mod.Flags2 = 0;
			default_pan_mod.Dest = (int) fluid_gen_type.GEN_PAN;

			// Target: pan - Amount: 500. The SF specs $8.4.6, p. 55 syas: "Amount = 1000 tenths of a percent".
			// The center value (64) corresponds to 50%, so it follows that amount = 50% x 1000/% = 500.
			default_pan_mod.Amount = 500.0f;

			/* SF2.01 page 55 section 8.4.7: MIDI continuous controller 11 to initial attenuation*/
			default_expr_mod.Src1 = 11;
			default_expr_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_CC /* CC=1 */
			                          | (int) fluid_mod_flags.FLUID_MOD_CONCAVE /* type=1 */
			                          | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                          | (int) fluid_mod_flags.FLUID_MOD_NEGATIVE;
			default_expr_mod.Src2 = 0;
			default_expr_mod.Flags2 = 0;
			default_expr_mod.Dest = (int) fluid_gen_type.GEN_ATTENUATION; /* Target: Initial attenuation */
			default_expr_mod.Amount = 960.0f; /* Amount: 960 */

			/* SF2.01 page 55 section 8.4.8: MIDI continuous controller 91 to Reverb send */
			default_reverb_mod.Src1 = 91;
			default_reverb_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_CC /* CC=1 */
			                            | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                            | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                            | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_reverb_mod.Src2 = 0;
			default_reverb_mod.Flags2 = 0;
			default_reverb_mod.Dest = (int) fluid_gen_type.GEN_REVERBSEND; /* Target: Reverb send */
			default_reverb_mod.Amount = 200; /* Amount: 200 ('tenths of a percent') */

			/* SF2.01 page 55 section 8.4.9: MIDI continuous controller 93 to Reverb send */
			default_chorus_mod.Src1 = 93;
			default_chorus_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_CC /* CC=1 */
			                            | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                            | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                            | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_chorus_mod.Src2 = 0;
			default_chorus_mod.Flags2 = 0;
			default_chorus_mod.Dest = (int) fluid_gen_type.GEN_CHORUSSEND; /* Target: Chorus */
			default_chorus_mod.Amount = 200; /* Amount: 200 ('tenths of a percent') */

			/* SF2.01 page 57 section 8.4.10 MIDI Pitch Wheel to Initial Pitch ... */
			default_pitch_bend_mod.Src1 = (int) fluid_mod_src.FLUID_MOD_PITCHWHEEL;
			default_pitch_bend_mod.Flags1 = (int) fluid_mod_flags.FLUID_MOD_GC /* CC =0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_BIPOLAR /* P=1 */
			                                | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_pitch_bend_mod.Src2 = (int) fluid_mod_src.FLUID_MOD_PITCHWHEELSENS;
			default_pitch_bend_mod.Flags2 = (int) fluid_mod_flags.FLUID_MOD_GC /* CC=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_LINEAR /* type=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_UNIPOLAR /* P=0 */
			                                | (int) fluid_mod_flags.FLUID_MOD_POSITIVE;
			default_pitch_bend_mod.Dest = (int) fluid_gen_type.GEN_PITCH; /* Destination: Initial pitch */
			default_pitch_bend_mod.Amount = 12700.0f; /* Amount: 12700 cents */
		}

		public static void AddDefaultMods(fluid_voice voice) {
			voice.fluid_voice_add_mod(default_vel2att_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.1  */
			voice.fluid_voice_add_mod(default_vel2filter_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.2  */
			voice.fluid_voice_add_mod(default_at2viblfo_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.3  */
			voice.fluid_voice_add_mod(default_mod2viblfo_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.4  */
			voice.fluid_voice_add_mod(default_att_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.5  */
			voice.fluid_voice_add_mod(default_pan_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.6  */
			voice.fluid_voice_add_mod(default_expr_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.7  */
			voice.fluid_voice_add_mod(default_reverb_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.8  */
			voice.fluid_voice_add_mod(default_chorus_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.9  */
			voice.fluid_voice_add_mod(default_pitch_bend_mod,
				fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.10 */
		}
	}

}
