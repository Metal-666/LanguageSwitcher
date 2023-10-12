using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace LanguageSwitcher;

public partial class SwitcherWindow : Window {

	public virtual List<TextBlock> LanguageBlocks { get; set; }
	public virtual List<Border> LanguageBlockBorders { get; set; }

	public SwitcherWindow() {

		InitializeComponent();

		LanguageBlocks = new() {

			LanguageLeft,
			LanguageTop,
			LanguageRight,
			LanguageBottom,

		};

		LanguageBlockBorders = new() {

			LanguageBorderLeft,
			LanguageBorderTop,
			LanguageBorderRight,
			LanguageBorderBottom,

		};

	}

	public virtual void SelectItem(int index) {

		if(index < 0) {

			Debug.WriteLine($"Failed to select item: index is {index}");

			return;

		}

		SetStyle(LanguageBlocks.Cast<FrameworkElement>(), "LanguageBlock", "LanguageBlockSelected");
		SetStyle(LanguageBlockBorders.Cast<FrameworkElement>(), "LanguageBlockBorder", "LanguageBlockBorderSelected");

		void SetStyle(IEnumerable<FrameworkElement> frameworkElements, string defaultStyle, string selectedStyle) {

			foreach(FrameworkElement frameworkElement in frameworkElements) {

				frameworkElement.Style = (Style) Resources[defaultStyle];

			}

			frameworkElements.ElementAt(index).Style = (Style) Resources[selectedStyle];

		}

	}

}