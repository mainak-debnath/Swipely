﻿using TimeTracker;

namespace TimeTracker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }

    }
}