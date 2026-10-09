using alfa_test_proj.UI;
using Photino.Blazor;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        
        builder.RootComponents.Add<App>("app");
        
        var app = builder.Build();
        app.MainWindow.SetTitle("Курсы валют ЦБ РФ").SetSize(1000, 700);
        
        AppDomain.CurrentDomain.UnhandledException += (_, e) => 
            app.MainWindow.ShowMessage("критическая ошибка", e.ExceptionObject.ToString());
        
        app.Run();
    }
}