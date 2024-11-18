﻿using Sandbox.Views;
using Canvas = DrawnUi.Maui.Views.Canvas;

namespace Sandbox
{


    public class MainPageAnomalyNodes : BasePage, IDisposable
    {
        Canvas Canvas;

        public void Dispose()
        {
            this.Content = null;
            Canvas?.Dispose();
        }

        public MainPageAnomalyNodes()
        {
            SkiaImageManager.ReuseBitmaps = true;

#if DEBUG
            HotReloadService.UpdateApplicationEvent += ReloadUI;
#endif
            Build();
        }

        private int _reloads;

        private SkiaLayout TreeLayout;

        void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = TreeLayout = new SkiaLayout
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.LightGray,
                }
            };

            _reloads++;

            TreeLayout.Children.Add(new SkiaLabel()
            {
                BackgroundColor = Colors.Black,
                Text = $"Reloaded {_reloads}",
                TextColor = Colors.Red
            });

            TreeLayout.LayoutIsReady += (s, a) =>
            {
                foreach (NodeViewModel node in GenerateNodes())
                {
                    AddNode(node);
                }
            };

            this.Content = Canvas;
        }

        private void ReloadUI(Type[] obj)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Build();
            });
        }


        public int GlobalOffset = 75;
        public const int NodeWidth = 40;
        public const int NodeHeight = 40;

        private void AddNode(NodeViewModel node)
        {
            Point pointWithOffset = node.Point.Offset(-(NodeWidth / 2), -(NodeHeight / 2)).Offset(GlobalOffset, GlobalOffset);

            TreeLayout.Children.Add(new SkiaButton
            {
                TranslationX = pointWithOffset.X,
                TranslationY = pointWithOffset.Y,
                Clicked = async (b, p) =>
                {
                    await DisplayAlert("", node.Icon, "Ok");
                },
                Children =
            {
                new SkiaShape
                {
                    WidthRequest = NodeWidth,
                    HeightRequest = NodeHeight,
                    Type = ShapeType.Circle,
                    Background = node.Color,
                    BackgroundColor = node.Color,
                    Content = new SkiaImage
                    {
                        LoadSourceOnFirstDraw = false,
                        Aspect = TransformAspect.AspectFit,
                        Source = "Images/" + node.Icon,
                    }
                }
            }
            });
        }

        private NodeViewModel[] GenerateNodes()
        {
            return new NodeViewModel[]
            {
            new NodeViewModel
            {
                Label = "Root",
                Icon = "nico.jpg",
                Color = Colors.Blue,
                Point = new Point(0, 0),
            },
            new NodeViewModel
            {
                Label = "Child 1",
                Icon = "nico.jpg",
                Color = Colors.Red,
                Point = new Point(100,50)
            },
            new NodeViewModel
            {
                Label = "Child 1",
                Icon = "nico.jpg",
                Color = Colors.Red,
                Point = new Point(100,0)
            },
            new NodeViewModel
            {
                Label = "Child 1",
                Icon = "nico.jpg",
                Color = Colors.Red,
                Point = new Point(100,150)
            },
            new NodeViewModel
            {
                Label = "Child 2",
                Icon = "nico.jpg",
                Color = Colors.Green,
                Point = new Point(100,100)
            },
            new NodeViewModel
            {
                Label = "nico",
                Icon = "nico.jpg",
                Color = Colors.Red,
                Point = new Point(200,0)
            },
            new NodeViewModel
            {
                Label = "nico",
                Icon = "nico.jpg",
                Color = Colors.Red,
                Point = new Point(300,0)
            },
            new NodeViewModel
            {
                Label = "GrandChild 1",
                Icon = "nico.jpg",
                Color = Colors.Yellow,
                Point = new Point(150,100)
            },
            new NodeViewModel
            {
                Label = "GrandChild 2",
                Icon = "nico.jpg",
                Color = Colors.Yellow,
                Point = new Point(150,150)
            },
            new NodeViewModel
            {
                Label = "GrandChild 3",
                Icon = "nico.jpg",
                Color = Colors.Yellow,
                Point = new Point(150,210)
            },
            new NodeViewModel
            {
                Label = "Child 3",
                Icon = "nico.jpg",
                Color = Colors.Orange,
                Point = new Point(0,200)
            }
            };
        }
    }

    public class NodeViewModel
    {
        required public string Label { get; set; }
        required public Color Color { get; set; }
        required public string Icon { get; set; }
        required public Point Point { get; set; }
    }

}
