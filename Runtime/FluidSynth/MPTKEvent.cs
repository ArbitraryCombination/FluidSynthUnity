// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable RedundantCaseLabel
using System.Collections.Generic;

namespace FluidSynth {

	/// <summary>
	/// MIDI command codes. Defined the action to be done with the message: note on/off, change instrument, ...
	/// Depending of the command selected, others properties must be set; Value, Channel, ....
	/// </summary>
	public enum MPTKCommand : byte {
		/// <summary>Note Off</summary>
		NoteOff = 0x80,

		/// <summary>Note On. Value contains the note to play between 0 and 127.</summary>
		NoteOn = 0x90,

		/// <summary>Key After-touch</summary>
		KeyAfterTouch = 0xA0,

		/// <summary>Control change. Controller contains iendtify the controller to change (Modulation, Pan, Bank Select ...). Value will contains the value of the controller between 0 and 127.</summary>
		ControlChange = 0xB0,

		/// <summary>Patch change. Value contains patch/preset/instrument value between 0 and 127. </summary>
		PatchChange = 0xC0,

		/// <summary>Channel after-touch</summary>
		ChannelAfterTouch = 0xD0,

		/// <summary>Pitch wheel change</summary>
		PitchWheelChange = 0xE0,

		/// <summary>Sysex message</summary>
		Sysex = 0xF0,

		/// <summary>Eox (comes at end of a sysex message)</summary>
		Eox = 0xF7,

		/// <summary>Timing clock (used when synchronization is required)</summary>
		TimingClock = 0xF8,

		/// <summary>Start sequence</summary>
		StartSequence = 0xFA,

		/// <summary>Continue sequence</summary>
		ContinueSequence = 0xFB,

		/// <summary>Stop sequence</summary>
		StopSequence = 0xFC,

		/// <summary>Auto-Sensing</summary>
		AutoSensing = 0xFE,

		/// <summary>Meta-event</summary>
		MetaEvent = 0xFF,
	}

	/// <summary>
	/// MidiController enumeration
	/// http://www.midi.org/techspecs/midimessages.php#3
	/// </summary>
	public enum MPTKController : byte {
		/// <summary>Bank Select (MSB)</summary>
		BankSelect = 0,

		/// <summary>Modulation (MSB)</summary>
		Modulation = 1,

		/// <summary>Breath Controller</summary>
		BreathController = 2,

		/// <summary>Foot controller (MSB)</summary>
		FootController = 4,

		/// <summary>Main volume</summary>
		MainVolume = 7,

		/// <summary>Pan</summary>
		Pan = 10,

		/// <summary>Expression</summary>
		Expression = 11,
		
		/// NOTE(jp): Added for consistency with other modulation code, but is not a true controller I guess? (see fluid_mod_src)
		PitchWheel = 14,

		/// <summary>Bank Select LSB ** not implemented **  </summary>
		BankSelectLsb = 32,

		/// <summary>Sustain</summary>
		Sustain = 64, // 0x40

		/// <summary>Portamento On/Off </summary>
		Portamento = 65,

		/// <summary>Sostenuto On/Off</summary>
		Sostenuto = 66,

		/// <summary>Soft Pedal On/Off</summary>
		SoftPedal = 67,

		/// <summary>Legato Footswitch</summary>
		LegatoFootswitch = 68,

		/// <summary>Reset all controllers</summary>
		ResetAllControllers = 121,

		/// <summary>All notes off</summary>
		AllNotesOff = 123,

		/// <summary>All sound off</summary>
		AllSoundOff = 120, // 0x78,
	}

	/// Midi Event class for MPTK. Use this class to generate Midi Music with MidiStreamPlayer or to read midi events from a Midi file with MidiLoad
	/// or to receive midi events from MidiFilePlayer OnEventNotesMidi.
	/// With this class, you can: play and stop a note, change instrument (preset, patch, ...), change some control as modulation
	/// See here https://paxstellar.fr/class-mptkevent
	public class MPTKEvent {

		/// Midi Command code. Defined the type of message (Note On, Control Change, Patch Change...)
		public MPTKCommand Command = MPTKCommand.NoteOn;

		/// <summary>
		/// Controller code. When the Command is ControlChange, contains the code fo the controller to change (Modulation, Pan, Bank Select ...). Value will contains the value of the controller.
		/// </summary>
		public MPTKController Controller = MPTKController.BankSelect;

		/// <summary>
		/// Contains a value between 0 and 127 in relation with the Command. For:
		///! @li @c   If Command = NoteOn then Value contains midi note. 60=C5, 61=C5#, ..., 72=C6, ....
		///! @li @c   If Command = ControlChange then Value contains controller value, see MPTKController
		///! @li @c   If Command = PatchChange then Value contains patch/preset/instrument value. See the current SoundFont to find value associated to each instrument.
		/// </summary>
		public int Value;

		/// <summary>
		/// Midi channel fom 0 to 15 (9 for drum)
		/// </summary>
		public int Channel = 0;

		public HiPreset Instrument;

		/// <summary>
		/// Velocity between 0 and 127
		/// </summary>
		public int Velocity = 127;

		/// <summary>
		/// Duration of the note in millisecond. Set -1 to play undefinitely.
		/// </summary>
		public long Duration = -1;

		/// <summary>
		/// Delay before playing the note in millisecond. New with V2.82, works only in Core mode.
		/// </summary>
		public long DelayMs = 0;

		/// <summary>
		/// Origin of the message. Midi ID if from Midi Input else zero. V2.83: rename source to Source et set public.
		/// </summary>
		private uint Source;

		/// <summary>
		/// List of voices associated to this Event for playing a NoteOn event.
		/// </summary>
		public List<fluid_voice> Voices;

		/// <summary>
		/// Check if playing of this midi event is over (all voices are OFF)
		/// </summary>
		public bool IsOver {
			get {
				var fluidVoices = Voices;
				if (fluidVoices == null) return true;
				foreach (fluid_voice voice in fluidVoices)
					if (voice.status != fluid_voice_status.FLUID_VOICE_OFF)
						return false;
				// All voices are off or empty
				return true;
			}
		}

		/// Build a string description of the Midi event
		public override string ToString() {
			string result;
			switch (Command) {
				case MPTKCommand.NoteOn:
					string sDuration = Duration == long.MaxValue ? "Inf." : Duration.ToString();
					result = $"NoteOn\tCh:{Channel:00}\tNote:{Value}\tDuration:{sDuration,-8}\tVelocity:{Velocity}";
					break;
				case MPTKCommand.NoteOff:
					sDuration = Duration == long.MaxValue ? "Inf." : Duration.ToString();
					result = $"NoteOff\tCh:{Channel:00}\tNote:{Value}\tDuration:{sDuration,-8}\tVelocity:{Velocity}";
					break;
				case MPTKCommand.PatchChange:
					result = $"Patch\tCh:{Channel:00}\tPatch:{Value}";
					break;
				case MPTKCommand.ControlChange:
					result = $"Control\tCh:{Channel:00}\tValue:{Value}\tControler:{Controller}";
					break;
				case MPTKCommand.KeyAfterTouch:
					result = $"KeyAfterTouch\tCh:{Channel:00}\tKey:{Value}\tValue:{Controller}";
					break;
				case MPTKCommand.ChannelAfterTouch:
					result = $"ChannelAfterTouch\tCh:{Channel:00}\tValue:{Value}";
					break;
				case MPTKCommand.PitchWheelChange:
					result = $"Pitch Wheel\tCh:{Channel:00}\tValue:{Value}";
					break;
				case MPTKCommand.AutoSensing:
					result = "Auto Sensing";
					break;
				default:
					result = $"Unknown Command\t:{(int) Command:X2} Ch:{Channel:00}\tNote:{Value}\tDuration:{Duration,2}\tVelocity:{Velocity}";
					break;
			}
			return result;
		}
	}

}
