using SharpHook.Native;
using SharpHook.Reactive;

using SimpleTrayIcon;

using System.Drawing;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace LanguageSwitcher;

public partial class App : Application {

	public virtual BehaviorSubject<int> LastKeyboardLayoutIndex { get; set; }

	public virtual List<string> KeyboardLayouts { get; set; } =
		new() {

			// UA
			"00020422",
			// EN
			"00000409",
			// RU
			"00000419",
			// JP
			"00000411"

		};

	public virtual SimpleReactiveGlobalHook Hook { get; set; }

	public virtual TrayMenu TrayMenu { get; set; }

	public virtual int CurrentKeyboardLayoutIndex {

		get {

			StringBuilder keyboardLayoutName = new();

			GetKeyboardLayoutName(keyboardLayoutName);

			return KeyboardLayouts.IndexOf(keyboardLayoutName.ToString());

		}

	}

	public App() {

		LastKeyboardLayoutIndex = new BehaviorSubject<int>(CurrentKeyboardLayoutIndex);

		#region Keyboard Hook
		// Two keys required to activate the Switcher.
		bool isWinPressed = false;
		bool isAltPressed = false;
		// Is the Switcher currently displayed?
		bool isActivated = false;
		// Was the Alt key pressed again after the Switcher was activated?
		bool isTappingAlt = false;

		Hook = new();

		Hook.KeyPressed
			.Select(args => args.Data.KeyCode)
			.Subscribe(keyCode => {
				// First, let's try to interpret the incoming key code as an arrow key.
				// If this key code is in range 37 - 40 (key codes for arrow keys)
				// then by subtracting the key code for left arrow (37) we should get a number in range 0 - 3.
				int possibleArrowCode = keyCode - KeyCode.VcLeft;
				// If we are indeed in range 0 - 3 and the Switcher is activated,
				// update the state using this number as a keyboard layout index.
				// Basically:
				// 0 -> left kb layout;
				// 1 -> top kb layout;
				// 2 -> right kb layout;
				// 3 -> bottom kb layout.
				if(possibleArrowCode is >= 0 and < 4 && isActivated) {

					UpdateState(possibleArrowCode);
					// Since we already know this key code was an arrow key code,
					// don't bother checking for other matches.
					return;

				}
				// If the key code wasn't an arrow key code, try to interpret it as a Win or Alt key press.
				switch(keyCode) {

					case KeyCode.VcLeftMeta: {

						isWinPressed = true;

						break;

					}

					case KeyCode.VcLeftAlt: {

						isAltPressed = true;

						break;

					}

					default: {
						// If the key code wasn't anything of the above, exit.
						return;

					}

				}
				// Since we didn't exit, the pressed key was either Win or Alt - we should update our state.
				UpdateState();

			});

		Hook.KeyReleased
			.Select(args => args.Data.KeyCode)
			.Subscribe(keyCode => {
				// Let's see if the released key was Win or Alt.
				switch(keyCode) {

					case KeyCode.VcLeftMeta: {

						isWinPressed = false;
						// If the Win key was released we can no longer tap Alt to switch layouts.
						isTappingAlt = false;

						break;

					}

					case KeyCode.VcLeftAlt: {
						// If both Win and Alt were pressed and we just released Alt,
						// treat that as tapping Alt.
						if(isWinPressed && isAltPressed) {

							isTappingAlt = true;

						}

						isAltPressed = false;

						break;

					}

					default: {
						// If the key code wasn't anything of the above, exit.
						return;

					}

				}
				// Since we didn't exit, the released key was either Win or Alt - we should update our state.
				UpdateState();

			});

		Hook.RunAsync();
		// This function updates the state of the Switcher.
		// It takes into account the current state of the Switcher and what keys are currently pressed.
		void UpdateState(int? keyboardLayoutIndex = null) {
			// If the Switcher is currently active...
			if(isActivated) {
				// ...and both of the activation keys are not pressed anymore:
				if(!isWinPressed && !isAltPressed) {
					// 1. Deactivate the Switcher;
					isActivated = false;
					// 2. Hide the Switcher window.
					// This must be down through a Dispatcher because we are in a background thread.
					Dispatcher.InvokeAsync(async () => {
						// Add a small delay to prevent accidental keypresses into newly focused window.
						await Task.Delay(25);

						((SwitcherWindow) MainWindow).Hide();

					});

				}
				// ...and the Alt key is being tapped:
				if(isAltPressed && isTappingAlt) {
					// 1. Up the current kb layout index by 1.
					keyboardLayoutIndex = LastKeyboardLayoutIndex.Value + 1;
					// 2. Make sure it's between 0 and 4.
					if(keyboardLayoutIndex >= 4) {

						keyboardLayoutIndex = 0;

					}

				}
				// ...and the kb layout index was set (either by passing it as a parameter,
				// or as a result of tapping Alt):
				if(keyboardLayoutIndex.HasValue) {
					// 1. Emit new index (should be received in the Switcher window).
					LastKeyboardLayoutIndex.OnNext(keyboardLayoutIndex.Value);
					// 2. Set the new kb layout.
					LoadKeyboardLayoutW(KeyboardLayouts[keyboardLayoutIndex.Value], 1);

				}

			}
			// If the Switcher is not active, but both activation buttons have just been pressed:
			else if(isWinPressed && isAltPressed) {
				// 1. Activate the Switcher.
				isActivated = true;
				// 2. Emit the current index (should be received in the Switcher window).
				LastKeyboardLayoutIndex.OnNext(CurrentKeyboardLayoutIndex);
				// 3. Resize, show and focus the Switcher window.
				// This must be down through a Dispatcher because we are in a background thread.
				Dispatcher.Invoke(() => {
					SwitcherWindow switcherWindow = (SwitcherWindow) MainWindow;

					// Based on https://stackoverflow.com/a/11552906/13027370
					uint thisWindowThreadId = GetWindowThreadProcessId(new WindowInteropHelper(switcherWindow).Handle, IntPtr.Zero);
					uint currentForegroundWindowThreadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);

					AttachThreadInput(currentForegroundWindowThreadId, thisWindowThreadId, true);

					switcherWindow.UpdateSizeAndPosition();
					switcherWindow.Show();
					switcherWindow.Activate();
					switcherWindow.Focus();

					AttachThreadInput(currentForegroundWindowThreadId, thisWindowThreadId, false);

				});

			}

		}
		#endregion

		#region Tray Menu
		using Stream iconStream =
			GetResourceStream(new Uri("pack://application:,,,/icon.ico")).Stream;

		TrayMenu = new(new Icon(iconStream),
								"Keyboard Layout Switcher");

		TrayMenuItem item = new() {

			Content = "Shutdown"

		};

		item.Click += (source, args) => Shutdown();

		TrayMenu.Items.Add(item);
		#endregion

	}

	protected override void OnExit(ExitEventArgs args) {

		Dispose();

		base.OnExit(args);

	}

	protected override void OnSessionEnding(SessionEndingCancelEventArgs args) {

		Dispose();

		base.OnSessionEnding(args);

	}

	public virtual void Dispose() {

		Hook.Dispose();
		TrayMenu.Dispose();

	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetKeyboardLayoutName(StringBuilder pwszKLID);

	[LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr LoadKeyboardLayoutW(string pwszKLID, uint flags);

	[LibraryImport("user32.dll")]
	private static partial IntPtr GetForegroundWindow();

	[LibraryImport("user32.dll")]
	private static partial uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

}