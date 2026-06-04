using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Net;
using System.Windows;

namespace Shaiya_Duff_Updater
{
  public class App : Application
  {
    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    [DebuggerNonUserCode]
    public void InitializeComponent() => this.StartupUri = new Uri("SplashScreen.xaml", UriKind.Relative);

    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    [STAThread]
    [DebuggerNonUserCode]
    public static void Main()
    {
      // .NET Framework 4.8 defaulta a TLS 1.0. Il server accetta solo TLS 1.2+.
      try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13; }
      catch { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }

      App app = new App();
      app.InitializeComponent();
      app.Run();
    }
  }
}

