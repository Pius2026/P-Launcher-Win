using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;

namespace PiusLauncher;

public class App : Application
{
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.15.0")]
	public void InitializeComponent()
	{
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.15.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run(new MainWindow());
	}
}
