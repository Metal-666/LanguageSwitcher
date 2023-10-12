using SharpHook.Native;
using SharpHook.Reactive;

using SimpleTrayIcon;

using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

using WpfScreenHelper;
using WpfScreenHelper.Enum;

namespace LanguageSwitcher;

public partial class App : Application {

	public virtual List<string> KeyboardLayouts { get; set; } =
		new() {

			"00020422",
			"00000409",
			"00000419",
			"00000411"

		};

	public virtual int LastKeyboardLayoutIndex { get; set; }

	public virtual SimpleReactiveGlobalHook Hook { get; set; }

	public virtual TrayMenu TrayMenu { get; set; }

	public App() {

		#region Keyboard Hook
		bool isWinPressed = false;
		bool isAltPressed = false;

		bool isActivated = false;

		bool isTappingAlt = false;

		Hook = new();

		Hook.KeyPressed
			.Select(args => args.Data.KeyCode)
			.Subscribe(keyCode => {

				int possibleArrowCode = keyCode - KeyCode.VcLeft;

				if(possibleArrowCode is >= 0 and < 4 && isActivated) {

					UpdateState(possibleArrowCode);

					return;

				}

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

						return;

					}

				}

				UpdateState();

			});

		Hook.KeyReleased
			.Select(args => args.Data.KeyCode)
			.Subscribe(keyCode => {

				switch(keyCode) {

					case KeyCode.VcLeftMeta: {

						isWinPressed = false;
						isTappingAlt = false;

						break;

					}

					case KeyCode.VcLeftAlt: {

						if(isWinPressed && isAltPressed) {

							isTappingAlt = true;

						}

						isAltPressed = false;

						break;

					}

					default: {

						return;

					}

				}

				UpdateState();

			});

		Hook.RunAsync();

		void UpdateState(int? keyboardLayoutIndex = null) {

			if(isActivated) {

				if(!isWinPressed && !isAltPressed) {

					Debug.WriteLine("Deactivated");

					isActivated = false;

					UpdateSwitcherWindow(switcherWindow =>
											switcherWindow.Hide());

				}

				if(isAltPressed && isTappingAlt) {

					if(++LastKeyboardLayoutIndex >= 4) {

						LastKeyboardLayoutIndex = 0;

					}

					keyboardLayoutIndex = LastKeyboardLayoutIndex;

				}

				if(keyboardLayoutIndex.HasValue) {

					UpdateSwitcherWindow(switcherWindow =>
											switcherWindow.SelectItem(keyboardLayoutIndex.Value));

					LoadKeyboardLayoutW(KeyboardLayouts[keyboardLayoutIndex.Value], 1);

				}

			}

			else if(isWinPressed && isAltPressed) {

				Debug.WriteLine("Activated");

				isActivated = true;

				StringBuilder keyboardLayoutName = new();

				GetKeyboardLayoutName(keyboardLayoutName);

				LastKeyboardLayoutIndex = KeyboardLayouts.IndexOf(keyboardLayoutName.ToString());

				UpdateSwitcherWindow(switcherWindow => {

					Screen currentScreen = Screen.FromPoint(MouseHelper.MousePosition);

					switcherWindow.MinHeight = currentScreen.Bounds.Height;
					switcherWindow.MinWidth = currentScreen.Bounds.Width;
					switcherWindow.SetWindowPosition(WindowPositions.Maximize, currentScreen);
					switcherWindow.Topmost = true;
					switcherWindow.Show();
					switcherWindow.Focus();
					switcherWindow.SelectItem(LastKeyboardLayoutIndex);

				});

			}

			void UpdateSwitcherWindow(Action<SwitcherWindow> action) =>
				Dispatcher.Invoke(() => action((SwitcherWindow) MainWindow));

		}

		#endregion

		#region Tray Menu
		using Stream iconStream =
			GetResourceStream(new Uri("pack://application:,,,/icon.ico")).Stream;

		TrayMenu = new(new Icon(iconStream),
								"Language Switcher");

		TrayMenuItem item = new() {

			Content = "Shutdown Language Switcher"

		};

		item.Click += (source, args) => Shutdown();

		TrayMenu.Items.Add(item);
		#endregion

	}

	protected override void OnExit(ExitEventArgs args) {

		Hook.Dispose();
		TrayMenu.Dispose();

	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetKeyboardLayoutName(StringBuilder pwszKLID);

	[LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr LoadKeyboardLayoutW(string pwszKLID, uint flags);

}