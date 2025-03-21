﻿using DrawnUi.Maui.Controls;

namespace DrawnUi.Maui.Draw
{
    public static partial class DrawnExtensions
    {
        public static void ConfigureHandlers(IMauiHandlersCollection handlers)
        {
#if !SKIA3
            handlers.AddHandler(typeof(SkiaViewAccelerated), typeof(FixedSKGLViewRenderer));
#endif
            handlers.AddHandler(typeof(DrawnUiBasePage), typeof(DrawnUiBasePageHandler));
            handlers.AddHandler(typeof(MauiEntry), typeof(MauiEntryHandler));
            handlers.AddHandler(typeof(MauiEditor), typeof(MauiEditorHandler));
        }
    }
}
