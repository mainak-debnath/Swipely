using System;
using Microsoft.UI.Xaml;

namespace TimeTracker.WinUI;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var app = new App(); // this refers to App : MauiWinUIApplication
        app.Run(args);
    }
}
