﻿using Sandbox.Views;
using Canvas = DrawnUi.Maui.Views.Canvas;

namespace Sandbox
{


    public class MainPageDevShape : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                HardwareAcceleration = HardwareAccelerationMode.Enabled,

                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Fill,
                WidthRequest = 300,
                HeightRequest = 300,
                BackgroundColor = Colors.White,

                Content = new SkiaLayout()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>()
                    {

                        new SkiaImage()
                        {
                            Source="car.png",
                            WidthRequest = 150,
                            HeightRequest = 150,
                            VerticalOptions = LayoutOptions.End,
                            Margin = 24,
                            Shadow = new Shadow()
                            {
                                Radius = 8,
                                Brush= Colors.Purple,
                                Offset = new (5,5)
                            }
                        }
                    }
                }
            };

 

            this.Content = Canvas;
        }

 

    }
}
