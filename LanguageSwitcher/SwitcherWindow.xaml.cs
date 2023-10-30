using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using WpfScreenHelper;
using WpfScreenHelper.Enum;

namespace LanguageSwitcher;

public partial class SwitcherWindow : Window {

	protected virtual List<TextBlock> KeyboardLayoutBlocks { get; set; }
	protected virtual List<Border> KeyboardLayoutBlockBorders { get; set; }

	protected virtual bool WindowPositionWasSet { get; set; }

	public SwitcherWindow() {

		InitializeComponent();

		KeyboardLayoutBlocks = new() {

			KeyboardLayoutLeft,
			KeyboardLayoutTop,
			KeyboardLayoutRight,
			KeyboardLayoutBottom,

		};

		KeyboardLayoutBlockBorders = new() {

			KeyboardLayoutBorderLeft,
			KeyboardLayoutBorderTop,
			KeyboardLayoutBorderRight,
			KeyboardLayoutBorderBottom,

		};
		// Whenever the kb layout index changes, update the UI to reflect the new index.
		((App) Application.Current).LastKeyboardLayoutIndex
									.Subscribe(index => Dispatcher.Invoke(() => {

										if(index < 0) {

											Debug.WriteLine($"Failed to select item: index is {index}");

											return;

										}

										SetStyle(KeyboardLayoutBlocks.Cast<FrameworkElement>(), "KeyboardLayoutBlock", "KeyboardLayoutBlockSelected");
										SetStyle(KeyboardLayoutBlockBorders.Cast<FrameworkElement>(), "KeyboardLayoutBlockBorder", "KeyboardLayoutBlockBorderSelected");
										// A helper function to reset all elements in a list to their default style
										// and then apply a "Selected" style to element at the current kb layout index.
										void SetStyle(IEnumerable<FrameworkElement> frameworkElements,
														string defaultStyle,
														string selectedStyle) {

											foreach(FrameworkElement frameworkElement in frameworkElements) {

												frameworkElement.Style = (Style) Resources[defaultStyle];

											}

											frameworkElements.ElementAt(index).Style = (Style) Resources[selectedStyle];

										}

									}));
		// We do this instead of setting the Visibility to Hidden in XAML
		// because if the window has never been shown, it won't be focused when we show it later
		// (and focusing it manually doesn't work either)
		// (and without focus all key presses will go into whatever window is actually focused).
		Dispatcher.InvokeAsync(Hide);

	}

	// Maximizes the Switcher on the screen were the mouse cursor is.
	public virtual void UpdateSizeAndPosition() {

		Screen currentScreen = Screen.FromPoint(MouseHelper.MousePosition);

		MinHeight = currentScreen.Bounds.Height;
		MinWidth = currentScreen.Bounds.Width;
		// There 2 bugs/intended-but-annoying-behaviours that we are working around here:
		// 1. Calling SetWindowPosition fails the first time you call it.
		// 2. Calling SetWindowPosition using Dispatcher.InvokeAsync works every time
		// but causes the window to jitter a little.
		// By using the WindowPositionWasSet boolean we can use the Dispatcher once
		// (and see the jitter only once) and after that directly use SetWindowPosition.
		if(!WindowPositionWasSet) {

			Dispatcher.InvokeAsync(() => this.SetWindowPosition(WindowPositions.Maximize, currentScreen));

			WindowPositionWasSet = true;

		}

		this.SetWindowPosition(WindowPositions.Maximize, currentScreen);

	}

}