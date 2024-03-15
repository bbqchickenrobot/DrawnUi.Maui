﻿using SkiaSharp.HarfBuzz;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Color = Microsoft.Maui.Graphics.Color;
using PropertyChangingEventArgs = Microsoft.Maui.Controls.PropertyChangingEventArgs;

namespace DrawnUi.Maui.Draw
{

    //todo
    //public enum UseRotationDirection
    //{
    //    None,
    //    UpsideDown,
    //    All
    //}

    //todo
    //1 draw visible paragraphs only
    // drawing only paragraphs visible in viewport and
    // caching paragraphs rendering one by one suing Operations cache type.

    //2 direction of the text to be able to draw it vertically 

    //3 accesibility features

    //4 textspan with any skiacontrol, this way the text will embrace images etc line in text editor


    [ContentProperty("Spans")]
    public partial class SkiaLabel : SkiaControl, ISkiaGestureListener
    {
        public static Color DebugColor = Colors.Transparent;
        //public static Color DebugColor = Color.Parse("#22ff0000");

        public static bool DebugSpans = false;

        public SkiaLabel() : base()
        {
            _spans.CollectionChanged += OnCollectionChanged;

            UpdateFont();
        }


        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            for (int i = 0; i < Spans.Count; i++)
                SetInheritedBindingContext(Spans[i], BindingContext);

        }

        public override string ToString()
        {
            if (Spans.Count > 0)
            {
                return string.Concat(Spans.Select(span => span.Text));
            }
            return this.Text;
        }

        #region SPANS

        public IList<TextSpan> Spans => _spans;

        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var newItems = e.NewItems as IEnumerable<TextSpan>;

            if (e.OldItems != null)
            {
                foreach (object item in e.OldItems)
                {
                    var bo = item as TextSpan;
                    if (bo != null)
                    {
                        bo.Parent = null;
                        bo.PropertyChanging -= OnItemPropertyChanging;
                        bo.PropertyChanged -= OnItemPropertyChanged;
                        if (newItems == null || newItems != null && !newItems.Contains(bo))
                        {
                            bo.Dispose();
                        }
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (object item in e.NewItems)
                {
                    var bo = item as TextSpan;
                    if (bo != null)
                    {
                        bo.Parent = this;
                        bo.PropertyChanging += OnItemPropertyChanging;
                        bo.PropertyChanged += OnItemPropertyChanged;
                    }

                }
            }

            OnPropertyChanged(nameof(Spans));
            SpansCollectionChanged?.Invoke(sender, e);
        }

        void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Spans));

        void OnItemPropertyChanging(object sender, PropertyChangingEventArgs e) => OnPropertyChanging(nameof(Spans));

        protected readonly SpanCollection _spans = new SpanCollection();

        public event NotifyCollectionChangedEventHandler SpansCollectionChanged;

        public class SpanCollection : ObservableCollection<TextSpan>
        {
            protected override void InsertItem(int index, TextSpan item) => base.InsertItem(index, item ?? throw new ArgumentNullException(nameof(item)));
            protected override void SetItem(int index, TextSpan item) => base.SetItem(index, item ?? throw new ArgumentNullException(nameof(item)));

            protected override void ClearItems()
            {
                var removed = new List<TextSpan>(this);
                base.ClearItems();
                base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed));
            }
        }

        #endregion

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName.IsEither(nameof(Spans)))
            {
                InvalidateMeasure();
            }
        }


        /// <summary>
        /// If strokePaint==null will not stroke
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="text"></param>
        /// <param name="textPaint"></param>
        /// <param name="strokePaint"></param>
        /// <param name="scale"></param>
        public void DrawText(SKCanvas canvas, float x, float y, string text,
            SKPaint textPaint,
            SKPaint strokePaint,
            SKPaint paintDropShadow,
            float scale)
        {
            if (paintDropShadow != null)
            {
                var offsetX = (int)(scale * DropShadowOffsetX);
                var offsetY = (int)(scale * DropShadowOffsetY);
                DrawTextInternal(canvas, text, x + offsetX, y + offsetY, paintDropShadow, scale);
            }

            if (strokePaint != null)
            {
                DrawTextInternal(canvas, text, x, y, strokePaint, scale);
            }

            DrawTextInternal(canvas, text, x, y, textPaint, scale);
        }

        protected virtual void DrawTextInternal(SKCanvas canvas, string text, float x, float y, SKPaint paint, float scale)
        {
            canvas.DrawText(text, (int)Math.Round(x), (int)Math.Round(y), paint);
        }

        #region RASTERIZE - NOT USED

        public static void DrawRasterizedText(SKCanvas canvas, float x, float y, string text, SKPaint textPaint, SKColor colorOutline)
        {
            using (var imageText = RasterizeText(text, textPaint, colorOutline))
            {

                var imageY = y - imageText.Height + 1;

                var padding = 0;

                if (colorOutline.Alpha != 0)
                    padding = 2;

                canvas.DrawImage(imageText, x - padding, imageY - padding);
            }
        }

        public static SKImage RasterizeText(string text, SKPaint paint, SKColor colorOutline)
        {

            using (var paintStrokeBigger = new SKPaint())
            using (var paintBigger = new SKPaint
            {
                TextSize = paint.TextSize * _scaleResampleText,
                Color = paint.Color,
                StrokeWidth = paint.StrokeWidth,
                IsStroke = paint.IsStroke,
                IsAntialias = paint.IsAntialias,
                Typeface = paint.Typeface,
            })
            {
                float paddingY = 0;
                float paddingX = 0;

                SKRect bounds = new SKRect();
                MeasureText(paint, text, ref bounds);

                SKRect boundsBigger = new SKRect();
                MeasureText(paintBigger, text, ref boundsBigger);

                if (colorOutline.Alpha != 0)
                {
                    paintStrokeBigger.TextSize = paint.TextSize * _scaleResampleText;
                    paintStrokeBigger.Color = colorOutline;
                    paintStrokeBigger.StrokeWidth = 4.5f * _scaleResampleText;
                    paintStrokeBigger.IsStroke = true;
                    paintStrokeBigger.IsAntialias = paint.IsAntialias;
                    paintStrokeBigger.Typeface = paint.Typeface;

                    SKRect boundsBiggerStroke = new SKRect();
                    MeasureText(paintStrokeBigger, text, ref boundsBiggerStroke);

                    paddingY = 4f * _scaleResampleText;
                    paddingX = 4f * _scaleResampleText;


                }


                var height = paint.TextSize + paddingY / _scaleResampleText * 2;
                var width = bounds.Width + paddingX / _scaleResampleText * 2;
                var info = new SKImageInfo((int)width, (int)height);

                var infoBigger = new SKImageInfo((int)(width * _scaleResampleText), (int)(height * _scaleResampleText));

                using (var surfaceBigger = SKSurface.Create(infoBigger))
                {
                    SKCanvas canvasRasterizedText = surfaceBigger.Canvas;

                    canvasRasterizedText.Clear();

                    if (colorOutline.Alpha != 0)
                    {
                        canvasRasterizedText.DrawText(text, 0 + paddingX, infoBigger.Height - paddingY, paintStrokeBigger);
                    }

                    canvasRasterizedText.DrawText(text, 0 + paddingX, infoBigger.Height - paddingY, paintBigger);

                    canvasRasterizedText.Flush();
                    using (var srcImg = surfaceBigger.Snapshot())
                    {
                        //downsample
                        using (var surface = SKSurface.Create(info))
                        using (var paintRescale = new SKPaint())
                        {
                            // high quality with antialiasing
                            paintRescale.IsAntialias = true;
                            paintRescale.FilterQuality = SKFilterQuality.High;


                            // draw the bitmap to fill the surface
                            surface.Canvas.DrawImage(srcImg, new SKRectI(0, 0, info.Width, info.Height), paintRescale);


                            //paintRescale.FilterQuality = SKFilterQuality.High;
                            //paintRescale.IsAntialias = false;
                            //paintRescale.IsDither = false;


                            //var kernelSize = new SKSizeI(3, 3);
                            //var kernelOffset = new SKPointI(1, 1);
                            //paintRescale.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                            //    kernelSize, KernelSharpen, 1.0f, 0f, kernelOffset, SKShaderTileMode.Clamp, false, null,null ); //1f, 0f, new SKPointI(1, 1),


                            surface.Canvas.Flush();

                            return surface.Snapshot();
                        }
                    }
                }
            }


        }

        #endregion

        public double Sharpen { get; set; }

        private static float _scaleResampleText = 1.0f;


        public static string Trail = "..";

        protected int RenderLimit = -1;

        protected void ResetTextCalculations()
        {
            IsCut = false;
            NeedMeasure = true;
            _lastDecomposed = null;
            RenderLimit = -1;
        }

        //protected override void OnSizeChanged()
        //{
        //	base.OnSizeChanged();

        //	Redraw();
        //}


        public TextLine[] Lines { get; protected set; }

        public float LineHeightPixels { get; protected set; }

        protected float _charMonoWidthPixels;

        void UpdateFontMetrics(SKPaint paint)
        {
            FontMetrics = paint.FontMetrics;
            LineHeightPixels = (float)Math.Round(-FontMetrics.Ascent + FontMetrics.Descent);//PaintText.FontSpacing;
            _fontUnderline = FontMetrics.UnderlinePosition.GetValueOrDefault();

            if (!string.IsNullOrEmpty(this.MonoForDigits))
            {
                _charMonoWidthPixels = MeasureTextWidthWithAdvance(paint, this.MonoForDigits, true);
            }
            else
            {
                _charMonoWidthPixels = 0;
            }
        }

        public SKPaint PaintDefault = new SKPaint
        {
            IsAntialias = true,
        };

        public SKPaint PaintStroke = new SKPaint
        {
            IsAntialias = true,
        };

        public SKPaint PaintShadow = new SKPaint
        {
            IsAntialias = true,
        };

        public SKPaint PaintDeco = new SKPaint
        {

        };


        public void MergeSpansForLines(
                TextSpan span,
                TextLine line,
                TextLine previousSpanLastLine)
        //merge first line with last from previous span
        {

            if (string.IsNullOrEmpty(previousSpanLastLine.Value))
            {
                return;
            }

            var spans = previousSpanLastLine.Spans.ToList();
            if (!string.IsNullOrEmpty(line.Value))
            {
                spans.AddRange(line.Spans);
                line.Width += previousSpanLastLine.Width;
            }
            else
            {
                line.Width = previousSpanLastLine.Width;
            }
            line.Spans = spans;

            line.Value = previousSpanLastLine.Value + line.Value;

            line.IsNewParagraph = previousSpanLastLine.IsNewParagraph;
            // var lastSpan = previousSpanLastLine.ApplySpans.LastOrDefault();

            /*
            if (string.IsNullOrEmpty(line.Value))
            {
                line.Value = previousSpanLastLine.Value;
                line.ApplySpans.AddRange(previousSpanLastLine.ApplySpans);
                line.Glyphs = previousSpanLastLine.Glyphs;
                line.Width = previousSpanLastLine.Width;
            }
            else
            {
                line.Value = previousSpanLastLine.Value + line.Value;
                line.ApplySpans.AddRange(previousSpanLastLine.ApplySpans);
                line.ApplySpans.Add(ApplySpan.Create(span,
                    lastSpan.End + 1,
                    lastSpan.End + line.Glyphs.Length));

                var characterPositions = new List<LineGlyph>();
                characterPositions.AddRange(previousSpanLastLine.Glyphs);
                var startAt = previousSpanLastLine.Width;
                foreach (var glyph in line.Glyphs)
                {
                    characterPositions.Add(LineGlyph.Move(glyph, glyph.Position + startAt));
                }
                line.Glyphs = characterPositions.ToArray();

                line.Width += previousSpanLastLine.Width;
            }
            */
        }

        public List<UsedGlyph> Glyphs { get; protected set; } = new();

        //todo
        bool AutoFindFont = false;

        public override ScaledSize Measure(float widthConstraint, float heightConstraint, float scale)
        {
            ReplaceFont();

            //background measuring or invisible or self measure from draw because layout will never pass -1
            if (IsMeasuring || !CanDraw || (widthConstraint < 0 || heightConstraint < 0))
            {
                return MeasuredSize;
            }

            var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);
            if (request.IsSame)
            {
                return MeasuredSize;
            }

            SetupDefaultPaint(scale);

            if (PaintDefault.Typeface == null)
            {
                UpdateFont();
                return MeasuredSize;
            }

            var constraints = GetMeasuringConstraints(request);

            IsMeasuring = true;

            var textWidthPixels = 0f;
            var textHeightPixels = 0f;

            var width = 0f;
            var height = 0f;

            //apply default props to default paint

            UpdateFontMetrics(PaintDefault);

            var usePaint = PaintDefault;

            if (Spans.Count == 0)
            {
                bool needsShaping = false;

                string text = null;
                Glyphs = GetGlyphs(Text, PaintDefault.Typeface);
                if (AutoFindFont)
                {
                    if (Glyphs != null && Glyphs.Count > 0)
                    {
                        if (UnicodeNeedsShaping(Glyphs[0].Symbol))
                        {
                            needsShaping = true;
                        }
                    }
                    text = Text;
                }
                else
                {
                    //replace unprintable symbols
                    if (Glyphs.Count > 0)
                    {
                        var textFiltered = "";

                        foreach (var glyph in Glyphs)
                        {
                            if (!glyph.IsAvailable)
                            {
                                textFiltered += FallbackCharacter;
                            }
                            else
                            {
                                textFiltered += glyph.Text;
                            }
                        }
                        text = textFiltered;
                    }
                    else
                    {
                        text = Text;
                    }
                }

                Lines = SplitLines(text, usePaint,
                    SKPoint.Empty,
                    (float)(constraints.Content.Width),
                    (float)(constraints.Content.Height),
                    MaxLines, needsShaping, null);
            }
            else
            {
                //Measure SPANS

                SKPoint offset = SKPoint.Empty;
                var mergedLines = new List<TextLine>();


                TextLine previousSpanLastLine = null;

                foreach (var span in Spans.ToList())
                {
                    if (string.IsNullOrEmpty(span.Text))
                        continue;

                    span.DrawingOffset = offset;

                    var paint = span.SetupPaint(scale, PaintDefault);

                    span.CheckGlyphsCanBeRendered(); //will auto-select typeface if needed

                    /*
                    var glyphAvailability = AreAllGlyphsAvailable(text, paint.Typeface);
                    var newText = new StringBuilder();
                    int glyphIndex = 0;

                    for (int i = 0; i < text.Length; i++)
                    {
                        // Handle surrogate pairs
                        int codePointSize = char.IsSurrogatePair(text, i) ? 2 : 1;

                        // Append either the character(s) or the fallback character
                        if (glyphAvailability[glyphIndex])
                        {
                            newText.Append(text, i, codePointSize);
                        }
                        else
                        {
                            newText.Append(FallbackCharacter);
                        }

                        // Move to the next glyph index and skip the low surrogate if we have a surrogate pair
                        glyphIndex++;
                        if (codePointSize == 2)
                        {
                            i++;
                        }
                    }

                    text = newText.ToString();
                    */

                    var lines = SplitLines(span.TextFiltered,
                            paint,
                            offset,
                            constraints.Content.Width,
                            constraints.Content.Height,
                            MaxLines, span.NeedShape, span);

                    if (lines != null && lines.Length > 0)
                    {
                        var firstLine = lines.First();
                        var lastLine = lines.Last();

                        //merge first one
                        if (previousSpanLastLine != null)
                        {
                            if (mergedLines.Count > 0)
                            {
                                //remove last, will be replaced by merged
                                mergedLines.Remove(previousSpanLastLine);
                            }
                            MergeSpansForLines(span, firstLine, previousSpanLastLine);
                        }

                        //todo
                        //if (!string.IsNullOrEmpty(line.Value))
                        //    line ApplySpans.Add(ApplySpan.Create(span, 0, line.Glyphs.Length - 1));

                        previousSpanLastLine = lastLine;
                        offset = new(lastLine.Width, 0);

                        mergedLines.AddRange(lines);
                    }
                    else
                    {
                        previousSpanLastLine = null;
                        offset = SKPoint.Empty;
                    }

                    //span.Lines = lines;

                }

                //last sanity pass
                if (!KeepSpacesOnLineBreaks && Spans.Count > 0)
                {
                    var index = 0;
                    foreach (var line in mergedLines)
                    {
                        index++;
                        if (index == mergedLines.Count) //do not process last line
                            break;

                        if (line.Value.Right(1) == " ")
                        {
                            var span = line.Spans.LastOrDefault();
                            //if (span.Span != null)
                            {
                                //remove last character from line, from last span and from last charactersposition
                                span.Text = span.Text.Substring(0, span.Text.Length - 1);
                                line.Value = line.Value.Substring(0, line.Value.Length - 1);
                                if (span.Glyphs != null)
                                {
                                    var newArray = span.Glyphs;
                                    if (!string.IsNullOrEmpty(line.Value))
                                    {
                                        //kill last glyph
                                        line.Width -= span.Size.Width - span.Glyphs[^1].Position;
                                        Array.Resize(ref newArray, newArray.Length - 1);
                                    }
                                    span.Glyphs = newArray;
                                }
                            }

                        }
                    }
                }

                Lines = mergedLines.ToArray();
            }


            if (Lines != null)
            {
                LinesCount = Lines.Length;
                var addParagraphSpacings = (Lines.Count(x => x.IsNewParagraph) - 1) * SpaceBetweenParagraphs;
                if (Lines.Length > 0)
                {
                    textWidthPixels = Lines.Max(x => x.Width); //todo width error inside split
                    textHeightPixels = (float)(LineHeightPixels * LinesCount +
                                               (LinesCount - 1) * SpaceBetweenLines + addParagraphSpacings);

                    ContentSize = ScaledSize.FromPixels(textWidthPixels, textHeightPixels, scale);

                    width = AdaptWidthConstraintToContentRequest(constraints.Request.Width, ContentSize, constraints.Margins.Left + constraints.Margins.Right);
                    height = AdaptHeightConstraintToContentRequest(constraints.Request.Height, ContentSize, constraints.Margins.Top + constraints.Margins.Bottom);
                }

            }
            else
            {
                width = request.Scale;
                height = LineHeightPixels;
                LinesCount = 0;
            }

            IsMeasuring = false;

            return SetMeasured(width, height, request.Scale);
        }


        public (int Limit, float Width) CutLineToFit(
            SKPaint paint,
            string textIn, float maxWidth)
        {
            SKRect bounds = new SKRect();
            var cycle = "";
            var limit = 0;
            float resultWidth = 0;

            var tail = string.Empty;
            if (LineBreakMode == LineBreakMode.TailTruncation)
                tail = Trail;

            textIn += tail;

            MeasureText(paint, textIn, ref bounds);

            if (bounds.Width > maxWidth && !string.IsNullOrEmpty(textIn))
            {

                for (int pos = 0; pos < textIn.Length; pos++)
                {
                    cycle = textIn.Left(pos + 1).TrimEnd() + tail;
                    MeasureText(paint, cycle, ref bounds);
                    if (bounds.Width > maxWidth)
                        break;
                    resultWidth = bounds.Width;
                    limit = pos + 1;
                }
            }

            return (limit, resultWidth);
        }


        public override void OnDisposing()
        {
            //todo check spans disposing!!!!
            Spans.Clear();

            _spans.CollectionChanged -= OnCollectionChanged;

            PaintDefault.Typeface = null;  //preserve cached font from disposing
            PaintDefault.Dispose();

            PaintStroke.Typeface = null; //preserve cached font from disposing
            PaintStroke.Dispose();

            PaintShadow.Typeface = null;  //preserve cached font from disposing
            PaintShadow.Dispose();

            PaintDeco.Dispose();

            base.OnDisposing();
        }



        public int LinesCount { get; protected set; } = 1;

        private int _fontWeight;

        //public override void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        //{

        //    if (NeedMeasure)
        //    {
        //        var adjustedDestination = CalculateLayout(destination, SizeRequest.Width, SizeRequest.Height, scale);
        //        Measure(adjustedDestination.Width, adjustedDestination.Height, scale);

        //    }

        //    base.Arrange(destination, MeasuredSize.Units.Width, MeasuredSize.Units.Height, scale);
        //}

        //protected virtual SKRect GetCacheArea()
        //{
        //     return new (
        //         DrawingRect.Left + AdjustCacheAreaPixels.Left,
        //         DrawingRect.Top+AdjustCacheAreaPixels.Top,
        //         DrawingRect.Right+AdjustCacheAreaPixels.Right,
        //         DrawingRect.Bottom+AdjustCacheAreaPixels.Bottom);
        //}

        protected Thickness AdjustCacheAreaPixels { get; set; }

        /*
        protected override void Draw(
            SkiaDrawingContext context,
            SKRect destination,
            float scale)
        {

            RenderingScale = scale;

            Arrange(destination, SizeRequest.Width, SizeRequest.Height, scale);

            if (!CheckIsGhost())
            {
                if (UseCache != SkiaCacheType.None)
                {
                    if (!UseRenderingObject(context, DrawingRect, scale))
                    {
                        //record to cache and paint 
                        CreateRenderingObjectAndPaint(context, DrawingRect, (ctx) =>
                        {
                            Paint(ctx, DrawingRect, scale, CreatePaintArguments());
                        });
                    }
                }
                else
                {
                    DrawWithClipAndTransforms(context, DrawingRect, true, true, (ctx) =>
                    {
                        Paint(ctx, DrawingRect, scale, CreatePaintArguments());
                    });
                }
            }

            FinalizeDraw(context, scale);
        }

        */

        //public override void PaintTintBackground(SKCanvas canvas, SKRect destination)
        //{
        //    //overriding to avoid applying fill gradient to background, in this control we use it for text only
        //    if (BackgroundColor != Colors.Transparent)
        //    {
        //        PaintDeco.Color = BackgroundColor.ToSKColor();
        //        PaintDeco.Style = SKPaintStyle.StrokeAndFill;
        //        PaintDeco.StrokeWidth = 0;
        //        canvas.DrawRect(destination, PaintDeco);
        //    }
        //}


        protected override void Paint(SkiaDrawingContext ctx, SKRect destination, float scale, object arguments)
        {
            base.Paint(ctx, destination, scale, arguments);

            var rectForChildren = ContractPixelsRect(destination, scale, Padding);

            if (Lines != null)
                DrawLines(ctx.Canvas, PaintDefault, SKPoint.Empty, Lines, rectForChildren, scale);
        }

        protected virtual void SpanPostDraw(
            SKCanvas canvas,
            TextSpan span,
            SKRect rect,
            float textY)
        {

            if (span.HasDecorations)
            {
                DrawSpanDecorations(canvas,
                    span,
                    rect.Left,
                    rect.Right,
                    textY);
            }

            if (DebugSpans)
            {
                PaintDeco.StrokeWidth = 0;
                PaintDeco.Color = GetRandomColor().WithAlpha(0.5f).ToSKColor();
                PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                canvas.DrawRect(rect, PaintDeco);
            }

        }

        protected void DrawSpanDecorations(
            SKCanvas canvas,
            TextSpan span,
            float xStart, float xEnd, float y)
        {
            PaintDeco.Style = SkiaSharp.SKPaintStyle.Stroke;
            PaintDeco.Color = span.TextColor.ToSKColor();
            if (span.Underline)
            {
                var moveY = span.Paint.FontMetrics.UnderlinePosition.GetValueOrDefault();
                if (moveY == 0)
                {
                    moveY = span.RenderingScale;
                }
                var yLevel = (float)Math.Round(y + moveY);
                PaintDeco.StrokeWidth = (float)(span.UnderlineWidth * span.RenderingScale);
                canvas.DrawLine(xStart, yLevel, xEnd, yLevel, PaintDeco);
            }
            if (span.Strikeout)
            {
                var moveY = span.Paint.FontMetrics.StrikeoutPosition.GetValueOrDefault();
                if (moveY == 0)
                {
                    moveY = -span.Paint.FontMetrics.XHeight / 2f;
                }
                var yLevel = (float)Math.Round(y + moveY);
                PaintDeco.StrokeWidth = (float)(span.StrikeoutWidth * span.RenderingScale);
                PaintDeco.Color = span.StrikeoutColor.ToSKColor();
                canvas.DrawLine(xStart, yLevel, xEnd, yLevel, PaintDeco);
            }
        }

        protected virtual void SetupDefaultPaint(float scale)
        {
            PaintDefault.TextSize = (float)Math.Round(FontSize * scale);
            PaintDefault.StrokeWidth = 0;
            PaintDefault.Typeface = this.TypeFace;
            PaintDefault.FakeBoldText = (this.FontAttributes & FontAttributes.Bold) != 0;
            //todo italic etc
        }

        public void DrawLines(SKCanvas canvas,
            SKPaint paintDefault,
            SKPoint startOffset,
            IEnumerable<TextLine> lines,
            SKRect rectDraw,
            double scale)
        {
            //apply dynamic properties that were not applied during measure
            paintDefault.Color = TextColor.ToSKColor();
            paintDefault.BlendMode = this.FillBlendMode;

            SKPaint paintStroke = null;

            if (StrokeColor.Alpha != 0 && StrokeWidth > 0)
            {
                PaintStroke.TextSize = paintDefault.TextSize * _scaleResampleText;
                PaintStroke.Color = StrokeColor.ToSKColor();
                PaintStroke.StrokeWidth = (float)(StrokeWidth * 2 * scale);
                PaintStroke.IsStroke = true;
                PaintStroke.IsAntialias = paintDefault.IsAntialias;
                PaintStroke.Typeface = paintDefault.Typeface;

                paintStroke = PaintStroke;
            }

            SKPaint paintDropShadow = null;

            if (this.DropShadowColor.Alpha != 0)
            {
                PaintShadow.TextSize = paintDefault.TextSize * _scaleResampleText;
                PaintShadow.Color = DropShadowColor.ToSKColor();
                PaintShadow.StrokeWidth = (float)(this.DropShadowSize * 2 * scale);
                PaintShadow.IsStroke = true;
                PaintShadow.IsAntialias = paintDefault.IsAntialias;
                PaintShadow.Typeface = paintDefault.Typeface;

                paintDropShadow = PaintShadow;
            }

            if (!GradientByLines)
            {
                SetupGradient(paintDefault, FillGradient, rectDraw);

                if (paintStroke != null)
                {
                    SetupGradient(paintStroke, StrokeGradient, rectDraw);
                }
            }

            if (DebugColor != Colors.Transparent)
            {
                PaintDeco.Color = DebugColor.ToSKColor();
                PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                PaintDeco.StrokeWidth = 0;
                canvas.DrawRect(rectDraw, PaintDeco);
            }


            //distance between descent and baseline
            //var moveToBaseline = -FontMetrics.Top; //LineHeightPixels - FontMetrics.Descent;
            var moveToBaseline = LineHeightPixels - FontMetrics.Descent;

            float baselineY = rectDraw.Top + moveToBaseline;//(float)(rectDraw.Top - FontMetrics.Top - adjust); //descent to text baseline

            if (this.VerticalTextAlignment == TextAlignment.End)
            {
                if (rectDraw.Height > ContentSize.Pixels.Height)
                {
                    baselineY += rectDraw.Height - ContentSize.Pixels.Height;
                }
            }
            else
            if (this.VerticalTextAlignment == TextAlignment.Center)
            {
                baselineY += (rectDraw.Height - ContentSize.Pixels.Height) / 2.0f;
            }

            var lineNb = 0;
            var processLines = lines.ToArray();

            foreach (var textSpan in Spans.ToList())
            {
                textSpan.Rects.Clear();
            }

            foreach (var line in processLines)
            {

                lineNb++;

                #region LAYOUT

                if (line.IsNewParagraph && lineNb > 1)
                {
                    baselineY += (float)SpaceBetweenParagraphs;
                }

                var alignedLineDrawingStartX = rectDraw.Left;
                if (lineNb == 1)
                {
                    alignedLineDrawingStartX += startOffset.X;
                }
                var enlargeSpaceCharacter = 0.0f;
                var fillCharactersOffset = 0.0f;
                if (this.HorizontalTextAlignment == DrawTextAlignment.Center)
                {
                    alignedLineDrawingStartX += (rectDraw.Width - line.Width) / 2.0f;
                    //System.Diagnostics.Debug.WriteLine($"[LINE D] {line.Width:0.0}");
                }
                else
                if (this.HorizontalTextAlignment == DrawTextAlignment.End)
                {
                    alignedLineDrawingStartX += rectDraw.Width - line.Width;
                }
                else
                if ((HorizontalTextAlignment == DrawTextAlignment.FillWords
                     || HorizontalTextAlignment == DrawTextAlignment.FillCharacters) && !line.IsLastInParagraph
                    || HorizontalTextAlignment == DrawTextAlignment.FillWordsFull
                    || HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull)
                {

                    var emptySpace = rectDraw.Width - line.Width;
                    if (lineNb == 1)
                    {
                        emptySpace = rectDraw.Width - (line.Width + startOffset.X);
                    }
                    if (emptySpace > 0)
                    {
                        if (HorizontalTextAlignment == DrawTextAlignment.FillWords || HorizontalTextAlignment == DrawTextAlignment.FillWordsFull)
                        {
                            var spaces = line.Value.Count(x => x == ' ');
                            if (spaces > 0)
                            {
                                enlargeSpaceCharacter = emptySpace / spaces;
                            }
                        }
                        else
                        if (HorizontalTextAlignment == DrawTextAlignment.FillCharacters || HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull)
                        {
                            fillCharactersOffset = emptySpace / (line.Value.Length - 1);
                        }
                    }
                }

                if (alignedLineDrawingStartX < rectDraw.Left)
                    alignedLineDrawingStartX = rectDraw.Left;

                line.Bounds = new SKRect(alignedLineDrawingStartX,
                    baselineY - moveToBaseline,
                    alignedLineDrawingStartX + line.Width,
                    baselineY - moveToBaseline + LineHeightPixels);

                #endregion

                //if (DebugColor != Colors.Transparent)
                //{
                //    PaintDeco.Color = DebugColor.ToSKColor();
                //    PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                //    PaintDeco.StrokeWidth = 0;

                //    canvas.DrawRect(line.Bounds, PaintDeco);
                //}

                if (GradientByLines)
                {
                    SetupGradient(paintDefault, FillGradient, line.Bounds);

                    if (paintStroke != null)
                    {
                        SetupGradient(paintStroke, StrokeGradient, line.Bounds);
                    }
                }

                //DRAW LINE SPANS
                float offsetX = 0;
                foreach (var lineSpan in line.Spans)
                {

                    SKRect rectPrecalculatedSpanBounds = SKRect.Empty;

                    var paint = paintDefault;
                    if (lineSpan.Span != null)
                    {
                        paint = lineSpan.Span.SetupPaint(scale, paintDefault);
                    }

                    var offsetAdjustmentX = 0.0f;

                    //draw shaped
                    if (lineSpan.NeedsShaping) //todo add stroke!
                    {
                        canvas.DrawShapedText(lineSpan.Text,
                            (float)Math.Round(alignedLineDrawingStartX + offsetX),
                            (float)Math.Round(baselineY),
                            paint);
                    }
                    else
                    //draw from glyphs
                    if (lineSpan.Glyphs != null)
                    {
                        //must have Glyphs!
                        var charIndex = 0;

                        float MoveOffsetAdjustmentX(float x, string p)
                        {
                            if (enlargeSpaceCharacter > 0 && p == " ")
                            {
                                x += enlargeSpaceCharacter;
                            }
                            else
                            if (fillCharactersOffset > 0 && charIndex > 0)
                            {
                                x += fillCharactersOffset;
                            }
                            return x;
                        }

                        //---
                        //precalculate rectangle for painting background

                        if (lineSpan.Span != null && lineSpan.Span.BackgroundColor != Colors.Transparent)
                        {
                            var x = offsetAdjustmentX;
                            foreach (var glyph in lineSpan.Glyphs)
                            {
                                x = MoveOffsetAdjustmentX(x, glyph.Text);
                            }

                            rectPrecalculatedSpanBounds = new SKRect(
                                alignedLineDrawingStartX + offsetX,
                                line.Bounds.Top,
                                alignedLineDrawingStartX + offsetX + lineSpan.Size.Width + x,
                                line.Bounds.Top + lineSpan.Size.Height);

                            PaintDeco.Color = lineSpan.Span.BackgroundColor.ToSKColor();
                            PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                            canvas.DrawRect(rectPrecalculatedSpanBounds, PaintDeco);
                        }

                        //---

                        foreach (var glyph in lineSpan.Glyphs)
                        {
                            var print = glyph.Text;

                            offsetAdjustmentX = MoveOffsetAdjustmentX(offsetAdjustmentX, print);

                            var posX = alignedLineDrawingStartX + offsetX + lineSpan.Glyphs[charIndex].Position + offsetAdjustmentX;

                            DrawCharacter(canvas, lineNb - 1, charIndex, print, posX,
                                baselineY, paint, paintStroke, paintDropShadow, line.Bounds, (float)scale);

                            charIndex++;
                        }

                    }
                    else
                    //fast code without characters positions
                    {
                        DrawText(canvas,
                            alignedLineDrawingStartX + offsetX,
                            baselineY,
                            line.Value,
                            paintDefault,
                            paintStroke,
                            paintDropShadow,
                            (float)scale);
                    }

                    var lineSpanRect = new SKRect(
                        alignedLineDrawingStartX + offsetX,
                        line.Bounds.Top,
                        alignedLineDrawingStartX + offsetX + lineSpan.Size.Width + offsetAdjustmentX,
                        line.Bounds.Top + lineSpan.Size.Height);

                    //lineSpan.DrawingRect = lineSpanRect;

                    if (lineSpan.Span != null)
                    {
                        lineSpan.Span.Rects.Add(lineSpanRect);
                        SpanPostDraw(canvas, lineSpan.Span, lineSpanRect, baselineY);
                    }

                    offsetX += lineSpan.Size.Width + offsetAdjustmentX;
                }

                baselineY += (float)(LineHeightPixels + SpaceBetweenLines);
            }

        }

        protected virtual void DrawTextLine()
        {

        }

        /// <summary>
        /// This is called when CharByChar is enabled
        /// You can override it to apply custom effects to every letter		/// </summary>
        /// <param name="canvas"></param>
        /// <param name="lineIndex"></param>
        /// <param name="letterIndex"></param>
        /// <param name="text"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="paint"></param>
        /// <param name="paintStroke"></param>
        /// <param name="scale"></param>
        protected virtual void DrawCharacter(SKCanvas canvas,
            int lineIndex, int letterIndex,
            string text, float x, float y, SKPaint paint, SKPaint paintStroke, SKPaint paintDropShadow, SKRect destination, float scale)
        {
            DrawText(canvas,
                x, y,
                text,
                paint, paintStroke, paintDropShadow, scale);
        }

        /// <summary>
        /// todo move this to some font info data block
        /// otherwise we wont be able to have multiple fonts 
        /// </summary>
        public double SpaceBetweenParagraphs
        {
            get
            {
                return LineHeightWithSpacing * ParagraphSpacing;
            }
        }

        public double SpaceBetweenLines
        {
            get
            {
                if (FontMetrics.Leading > 0)
                {
                    return FontMetrics.Leading * LineSpacing;
                }
                else
                {
                    if (LineSpacing != 1)
                    {
                        double defaultLeading = LineHeightPixels * 0.1;
                        return defaultLeading * LineSpacing;
                    }
                    return 0;
                }
            }
        }



        /// <summary>
        /// todo move this to some font info data block
        /// otherwise we wont be able to have multiple fonts 
        /// </summary>
        public double LineHeightWithSpacing
        {
            get
            {
                return LineHeightPixels + SpaceBetweenLines;
            }
        }

        //public float CalculatedTextHeight(SKPaint paint, float scale)
        //{
        //    var textHeight = (float)(textPaint.TextSize * scale);
        //}


        protected string _fontFamily;

        private static IFontRegistrar _registrar;
        public static IFontRegistrar FontRegistrar
        {
            get
            {
                if (_registrar == null)
                {

                    _registrar = Super.Services.GetService<IFontRegistrar>();

                    //_registrar = Super.Services.GetService<IFontManager>();


                }
                return _registrar;
            }
        }

        const string TypicalFontAssetsPath = "../Fonts/";

        static Stream GetEmbeddedResourceStream(string filename, System.Reflection.Assembly assembly = null)
        {
            if (assembly == null)
            {
                assembly = Super.App.GetType().Assembly;
            }

            var resourceNames = assembly.GetManifestResourceNames();
            var searchName = "." + filename;

            foreach (var name in resourceNames)
            {
                if (name.EndsWith(searchName, StringComparison.CurrentCultureIgnoreCase))
                    return assembly.GetManifestResourceStream(name)!;
            }

            throw new FileNotFoundException($"Resource ending with {filename} not found.");
        }

        /// <summary>
        /// A new TypeFace was set
        /// </summary>
        protected virtual void OnFontUpdated()
        {
            NeedMeasure = true;
        }

        protected bool ShouldUpdateFont { get; set; }

        protected virtual async void UpdateFont()
        {
            if (
                (TypeFace == null && !string.IsNullOrEmpty(_fontFamily)) ||
                _fontFamily != FontFamily || _fontWeight != FontWeight
                || (_fontFamily == null && TypeFace == null))
            {
                _fontFamily = FontFamily;
                _fontWeight = FontWeight;

                _replaceFont = await SkiaFontManager.Instance.GetFont(_fontFamily, _fontWeight);
            }

            InvalidateMeasure();
        }

        protected void ReplaceFont()
        {
            if (_replaceFont != null)
            {
                TypeFace = _replaceFont;
                _replaceFont = null;
                OnFontUpdated();
            }
        }

        protected float _fontUnderline;

        public bool IsCut { get; protected set; }

        private DecomposedText _lastDecomposed;

        private TextLine[] SplitLines(string text,
            SKPaint paint,
            SKPoint firstLineOffset,
            float maxWidth,
            float maxHeight,
            int maxLines,
            bool needsShaping,
            TextSpan span)
        {
            if (string.IsNullOrEmpty(text) || paint.Typeface == null)
            {
                return null;
            }

            if (span != null)
            {
                needsShaping = span.NeedShape;
            }

            bool needCalc = true;
            DecomposedText decomposedText = null;
            var autosize = this.AutoSize;
            var autoSizeFontStep = 0.1f;

            if (UsingFontSize > 0 && (AutoSize == AutoSizeType.FitFillHorizontal || AutoSize == AutoSizeType.FitFillVertical))
            {
                paint.TextSize = (float)UsingFontSize; //use from last time
                UpdateFontMetrics(paint);
            }

            bool calculatingMask = false;
            var measureText = text;

            if (!string.IsNullOrEmpty(AutoSizeText))
            {
                calculatingMask = true;
                measureText = AutoSizeText;
            }

            while (needCalc)
            {
                decomposedText = DecomposeText(measureText, paint, firstLineOffset, maxWidth, maxHeight, maxLines, needsShaping, span);

                if (autosize != AutoSizeType.None && maxWidth > 0 && maxHeight > 0)
                {

                    if ((AutoSize == AutoSizeType.FitHorizontal || AutoSize == AutoSizeType.FitFillHorizontal)
                        && (decomposedText.CountParagraphs != decomposedText.Lines.Length || decomposedText.WasCut))
                    {
                        autosize = AutoSizeType.FitHorizontal;
                    }
                    else
                    if ((AutoSize == AutoSizeType.FitVertical || AutoSize == AutoSizeType.FitFillVertical)
                        && decomposedText.WasCut)
                    {
                        autosize = AutoSizeType.FitVertical;
                    }
                    else
                    if ((AutoSize == AutoSizeType.FillVertical || AutoSize == AutoSizeType.FitFillVertical)
                        && decomposedText.HasMoreVerticalSpace >= 3)
                    {
                        autosize = AutoSizeType.FillVertical;
                    }
                    else
                    if ((AutoSize == AutoSizeType.FillHorizontal || AutoSize == AutoSizeType.FitFillHorizontal)
                        && decomposedText.HasMoreHorizontalSpace >= 3)
                    {
                        autosize = AutoSizeType.FillHorizontal;
                    }
                    else
                    {
                        autosize = AutoSizeType.None;
                    }

                    if (autosize == AutoSizeType.FitVertical || autosize == AutoSizeType.FitHorizontal)
                    {
                        if (paint.TextSize == 0)
                        {
                            //wtf just happened
                            Trace.WriteLine($"[SkiaLabel] Error couldn't fit text '{this.Text}' inside label width {this.Width}");
                            if (Debugger.IsAttached)
                                Debugger.Break();
                            paint.TextSize = 12;
                            needCalc = false;
                        }
                        paint.TextSize -= autoSizeFontStep;
                        UpdateFontMetrics(PaintDefault);
                    }
                    else
                    if (autosize == AutoSizeType.FillVertical || autosize == AutoSizeType.FillHorizontal)
                    {
                        paint.TextSize += autoSizeFontStep;
                        UpdateFontMetrics(PaintDefault);
                    }
                }
                else
                {
                    needCalc = false;
                    if (calculatingMask)
                    {
                        calculatingMask = false;
                        measureText = text;
                        decomposedText = DecomposeText(measureText, paint, firstLineOffset, maxWidth, maxHeight, maxLines, needsShaping, span);
                    }
                }

                decomposedText.AutoSize = autosize;

                if (_lastDecomposed != null && autosize == AutoSizeType.None) //autosize ended
                {
                    if (_lastDecomposed.AutoSize == AutoSizeType.FillHorizontal)
                    {
                        decomposedText = _lastDecomposed;
                    }
                    else
                    if (_lastDecomposed.AutoSize == AutoSizeType.FitHorizontal)
                    {
                        //var stop = _lastDecomposed.Lines;
                    }
                }

                _lastDecomposed = decomposedText;
            }

            IsCut = decomposedText.WasCut;
            UsingFontSize = paint.TextSize;

            return decomposedText.Lines;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public double UsingFontSize { get; set; }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MeasureTextWidthWithAdvance(SKPaint paint, string text, bool useCache)
        {
            return paint.MeasureText(text);

            /*
            ConcurrentDictionary<string, float> typefaceCache = null;
            var thisFont = new GlyphSizeDefinition
            {
                Typeface = paint.Typeface,
                FontSize = paint.TextSize
            };

            if (!MeasuredGlyphsWidthWithAdvance.TryGetValue(thisFont, out typefaceCache))
            {
                typefaceCache = new();
                MeasuredGlyphsWidthWithAdvance.TryAdd(thisFont, typefaceCache);
            }

            float width = 0f;
            if (!typefaceCache.TryGetValue(text, out width))
            {
                width = paint.MeasureText(text);
            }
            */

            //if (paint.TextSkewX != 0)
            //{
            //    var rect = SKRect.Empty;
            //    MeasureText(paint, text, ref rect);
            //    float additionalWidth = Math.Abs(paint.TextSkewX) * rect.Height;
            //    width += additionalWidth;
            //}

        }

        //static float GetTextTransformsWidthCorrection(SKPaint paint, string text)
        //{
        //    var additionalWidth = 0f;
        //    if (paint.TextSkewX != 0)
        //    {
        //        var lastCharRect = SKRect.Empty;
        //        MeasureText(paint, text, ref lastCharRect);
        //        additionalWidth = Math.Abs(paint.TextSkewX) * lastCharRect.Height;
        //    }
        //    return additionalWidth;
        //}



        //public static float MeasureTextWidthWithAdvanceAndTransforms(SKPaint paint, string text)
        //{
        //    float width = paint.MeasureText(text);

        //    if (paint.TextSkewX != 0 && !string.IsNullOrEmpty(text))
        //    {
        //        var lastCharRect = SKRect.Empty;
        //        MeasureText(paint, text.Last().ToString(), ref lastCharRect);
        //        float additionalWidth = Math.Abs(paint.TextSkewX) * lastCharRect.Height;
        //        width += additionalWidth;
        //    }

        //    return width;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MeasureTextWidth(SKPaint paint, string text)
        {
            var rect = SKRect.Empty;
            MeasureText(paint, text, ref rect);
            return rect.Width;
        }

        /// <summary>
        /// Accounts paint transforms like skew etc
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="text"></param>
        /// <param name="bounds"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MeasureText(SKPaint paint, string text, ref SKRect bounds)
        {
            paint.MeasureText(text, ref bounds);

            if (paint.TextSkewX != 0)
            {
                float additionalWidth = Math.Abs(paint.TextSkewX) * paint.TextSize;
                bounds.Right += additionalWidth; //notice passed by ref struct will be modified
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LastNonSpaceIndex(string text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }
            return -1;
        }


        public static List<bool> AreAllGlyphsAvailable(string text, SKTypeface typeface)
        {
            var glyphIds = typeface.GetGlyphs(text);
            var results = new List<bool>(glyphIds.Length); // Use the length of glyphIds instead

            int glyphIndex = 0;
            for (int i = 0; i < text.Length; i++)
            {
                //int codePoint = char.ConvertToUtf32(text, i);

                // Check if it's a high surrogate and increment to skip the low surrogate.
                if (char.IsHighSurrogate(text[i]))
                {
                    i++;
                }

                bool glyphExists = glyphIds[glyphIndex] != 0;
                results.Add(glyphExists);

                // Since each code point maps to a single glyph, increment the glyph index separately.
                glyphIndex++;
            }

            return results;
        }


        //public static SKSize GetResultSize(SKShaper.Result result)
        //{
        //    if (result == null || result.Points.Length == 0)
        //        throw new ArgumentNullException(nameof(result));

        //    float minX = float.MaxValue;
        //    float maxX = float.MinValue;
        //    float minY = float.MaxValue;
        //    float maxY = float.MinValue;

        //    // Calculate the bounds by considering each glyph's position and advance
        //    for (var i = 0; i < result.Codepoints.Length; i++)
        //    {
        //        var point = result.Points[i];

        //        // Update the minX and minY for the start of the glyph
        //        minX = Math.Min(minX, point.X);
        //        minY = Math.Min(minY, point.Y);

        //        // Since this is the positioned glyph, maxX and maxY should consider the glyph width/height which are scaled advances
        //        maxX = Math.Max(maxX, point.X);
        //        maxY = Math.Max(maxY, point.Y);
        //    }

        //    // The width is the difference between the max X and min X values, plus the width of the last glyph
        //    float width = maxX - minX + result.Width;

        //    // The height is the difference between the max Y and min Y values
        //    float height = maxY - minY;

        //    return new SKSize(result.Width, height);


        //    return new SKSize(width, height);
        //}

        public static SKSize GetResultSize(SKShaper.Result result)
        {
            if (result == null || result.Points.Length == 0)
                throw new ArgumentNullException(nameof(result));

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            // Calculate the bounds by considering each glyph's Y position
            for (var i = 0; i < result.Points.Length; i++)
            {
                var point = result.Points[i];

                // Update minY and maxY based on the glyph's position
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            // The height is the difference between the max Y and min Y values
            float height = maxY - minY;

            // Use the result.Width directly for the width
            return new SKSize(result.Width, height);
        }


        // 			using var shaper = new SKShaper(paint.GetFont().Typeface);
        public static SKShaper.Result GetShapedText(SKShaper shaper, string text, float x, float y, SKPaint paint)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            if (shaper == null)
                throw new ArgumentNullException(nameof(shaper));
            if (paint == null)
                throw new ArgumentNullException(nameof(paint));

            using var font = paint.ToFont();
            font.Typeface = shaper.Typeface;

            // shape the text
            var result = shaper.Shape(text, x, y, paint);

            return result;

            /*

            // create the text blob
            using var builder = new SKTextBlobBuilder();
            var run = builder.AllocatePositionedRun(font, result.Codepoints.Length);

            // copy the glyphs
            var g = run.GetGlyphSpan();
            var p = run.GetPositionSpan();
            for (var i = 0; i < result.Codepoints.Length; i++)
            {
                g[i] = (ushort)result.Codepoints[i];
                p[i] = result.Points[i];
            }

            // build
            using var textBlob = builder.Build();

            // adjust alignment
            var xOffset = 0f;
            if (paint.TextAlign != SKTextAlign.Left) {
                var width = result.Width;
                if (paint.TextAlign == SKTextAlign.Center)
                    width *= 0.5f;
                xOffset -= width;
            }

            */


            //canvas.DrawText(textBlob, xOffset, 0, paint);
        }



        public static List<UsedGlyph> GetGlyphs(string text, SKTypeface typeface)
        {
            var glyphIds = typeface.GetGlyphs(text);
            var results = new List<UsedGlyph>(glyphIds.Length);
            int glyphIndex = 0;

            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    int codePoint = char.ConvertToUtf32(text, i);

                    bool isHighSurrogate = char.IsHighSurrogate(text[i]);

                    // Get the text for the glyph, which may be one or two characters long.
                    string glyphText = text.Substring(i, isHighSurrogate ? 2 : 1);

                    if (isHighSurrogate)
                    {
                        i++;
                    }
                    var id = glyphIds[glyphIndex];

                    results.Add(new UsedGlyph
                    {
                        Id = id,
                        Text = glyphText,
                        IsAvailable = id != 0 || IsGlyphAlwaysAvailable(glyphText),
                        Symbol = codePoint
                    });
                    glyphIndex++;
                }

            }

            return results;
        }

        public static bool IsGlyphAlwaysAvailable(string glyphText)
        {
            return glyphText == "\n";
        }

        protected (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(SKPaint paint, string text, bool needsShaping)
        {
            if (string.IsNullOrEmpty(text))
                return (0.0f, null);

            var glyphs = GetGlyphs(text, paint.Typeface);

            var positions = new List<LineGlyph>();
            float value = 0.0f;
            float offsetX = 0f;

            if (needsShaping)
            {
                using var shaper = new SKShaper(paint.Typeface);
                var result = GetShapedText(shaper, text, 0, 0, paint);
                var measured = GetResultSize(result);
                return (measured.Width, null);
            }

            if (_charMonoWidthPixels > 0)
            {
                foreach (var glyph in glyphs)
                {
                    var print = glyph.Text;

                    var centerOffset = 0.0f;

                    bool mono = glyph.IsNumber();

                    var thisWidth = MeasureTextWidthWithAdvance(paint, print, true);

                    if (mono)
                    {
                        //align to center of available space
                        centerOffset = (_charMonoWidthPixels - thisWidth) / 2.0f;
                    }

                    var valueOffset = offsetX + centerOffset;
                    positions.Add(LineGlyph.FromGlyph(glyph, valueOffset, thisWidth));

                    if (mono)
                    {
                        offsetX += _charMonoWidthPixels;
                        value += _charMonoWidthPixels;
                    }
                    else
                    {
                        offsetX += thisWidth;
                        value += thisWidth;
                    }
                }

                //  System.Diagnostics.Debug.WriteLine($"[LINE M] {value:0.0}");
                return (value, positions.ToArray());
            }


            if (Spans.Count > 0 || CharacterSpacing != 1f
                || HorizontalTextAlignment == DrawTextAlignment.FillWordsFull
                || HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull
                || HorizontalTextAlignment == DrawTextAlignment.FillWords
                || HorizontalTextAlignment == DrawTextAlignment.FillCharacters)
            {
                var spacingModifier = (int)Math.Round(MeasuredSize.Scale * (CharacterSpacing - 1));

                var pos = 0;
                var addAtIndex = -1;
                if (paint.TextSkewX != 0)
                {
                    addAtIndex = LastNonSpaceIndex(text);
                }

                foreach (var glyph in glyphs)
                {
                    var thisWidth = MeasureTextWidthWithAdvance(paint, glyph.Text, true);
                    {
                        if (pos == addAtIndex)
                        {
                            var additionalWidth = (int)Math.Round(Math.Abs(paint.TextSkewX) * paint.TextSize / 2f);

                            thisWidth += additionalWidth;
                        }
                    }

                    positions.Add(LineGlyph.FromGlyph(glyph, offsetX, thisWidth));

                    offsetX += thisWidth + spacingModifier;
                    value += thisWidth + spacingModifier;

                    pos++;
                }

                //  System.Diagnostics.Debug.WriteLine($"[LINE M] {value:0.0}");
                return (value - spacingModifier, positions.ToArray());
            }

            var simpleValue = MeasureTextWidthWithAdvance(paint, text, false);

            //System.Diagnostics.Debug.WriteLine($"[LINE S] {simpleValue:0.0}");
            return (simpleValue, null);
        }



        #region Glyphs availability



        public static readonly Dictionary<UnicodeCategory, string> FallbackFonts = new Dictionary<UnicodeCategory, string>
        {
            { UnicodeCategory.UppercaseLetter, "Noto Sans" },
            { UnicodeCategory.LowercaseLetter, "Noto Sans" },
            { UnicodeCategory.OtherLetter, "Noto Sans Arabic" },
            { UnicodeCategory.MathSymbol, "Noto Sans Symbols" },
            { UnicodeCategory.CurrencySymbol, "Noto Sans Symbols" },
            { UnicodeCategory.ModifierSymbol, "Noto Sans Symbols" },
        };

        public static string GetFallbackFontName(char character, SKTypeface currentTypeface)
        {
            // Determine the Unicode category of the character
            UnicodeCategory unicodeCategory = char.GetUnicodeCategory(character);

            // Check if the current typeface supports the character
            var glyphIndices = currentTypeface.GetGlyphs(character.ToString());
            if (glyphIndices.Length > 0 && glyphIndices[0] != 0)
            {
                // Glyph exists in the current font
                return null;
            }

            // Glyph doesn't exist, need to find an appropriate fallback
            if (FallbackFonts.TryGetValue(unicodeCategory, out string fallbackFont))
            {
                return fallbackFont;
            }

            // General fallback if no specific font found
            return "Noto Sans";
        }

        protected static Dictionary<(char, SKTypeface), bool> GlyphAvailabilityCache = new Dictionary<(char, SKTypeface), bool>();



        //public static List<bool> AreAllGlyphsAvailable(string text, SKTypeface typeface)
        //{
        //    var glyphIds = typeface.GetGlyphs(text);
        //    var results = new List<bool>(text.Length);

        //    for (int i = 0; i < text.Length; i++)
        //    {
        //        if (text[i] == '\r' || text[i] == '\n' || glyphIds[i] != 0)
        //        {
        //            results.Add(true);
        //        }
        //        else
        //        {
        //            results.Add(false);  // Glyph is missing
        //        }
        //    }

        //    return results;
        //}




        #endregion

        List<string> SplitLineToWords(string line, char space)
        {
            if (line == space.ToString())
            {
                return new()
                {
                    line
                };
            }
            string GetSpaces(string str, bool leading)
            {
                var spaces = leading ? str.TakeWhile(c => c == space).ToArray()
                    : str.Reverse().TakeWhile(c => c == space).ToArray();
                return new string(spaces);
            }

            var leadingSpaces = GetSpaces(line, leading: true);
            var trailingSpaces = GetSpaces(line, leading: false);

            // Now trim the line and split by space, without removing empty entries
            var trimmedLine = line.Trim();
            var words = trimmedLine.Split(new[] { space }, StringSplitOptions.None).Reverse().ToList();

            // words list is inverted!
            if (leadingSpaces.Length > 0) words.Add(leadingSpaces);
            if (trailingSpaces.Length > 0) words.Insert(0, trailingSpaces);

            //if (words.Count > 0 && NeedsRTL(words[0]))
            //{
            //    words.Reverse();
            //}

            return words;
        }

        public static bool UnicodeNeedsShaping(int unicodeCharacter)
        {
            if (EmojiData.IsEmoji(unicodeCharacter))
                return true;

            // Emoji skin tone modifiers (Fitzpatrick scale)
            if (unicodeCharacter >= 0x1F3FB && unicodeCharacter <= 0x1F3FF) return true;

            // Arabic Unicode range
            if (unicodeCharacter >= 0x0600 && unicodeCharacter <= 0x06FF) return true;

            // Syriac Unicode range
            if (unicodeCharacter >= 0x0700 && unicodeCharacter <= 0x074F) return true;

            // Thaana Unicode range
            if (unicodeCharacter >= 0x0780 && unicodeCharacter <= 0x07BF) return true;

            // Devanagari Unicode range
            if (unicodeCharacter >= 0x0900 && unicodeCharacter <= 0x097F) return true;

            // Bengali Unicode range
            if (unicodeCharacter >= 0x0980 && unicodeCharacter <= 0x09FF) return true;

            // Gurmukhi Unicode range
            if (unicodeCharacter >= 0x0A00 && unicodeCharacter <= 0x0A7F) return true;

            // Gujarati Unicode range
            if (unicodeCharacter >= 0x0A80 && unicodeCharacter <= 0x0AFF) return true;

            // Oriya Unicode range
            if (unicodeCharacter >= 0x0B00 && unicodeCharacter <= 0x0B7F) return true;

            // Tamil Unicode range
            if (unicodeCharacter >= 0x0B80 && unicodeCharacter <= 0x0BFF) return true;

            // Telugu Unicode range
            if (unicodeCharacter >= 0x0C00 && unicodeCharacter <= 0x0C7F) return true;

            // Kannada Unicode range
            if (unicodeCharacter >= 0x0C80 && unicodeCharacter <= 0x0CFF) return true;

            // Malayalam Unicode range
            if (unicodeCharacter >= 0x0D00 && unicodeCharacter <= 0x0D7F) return true;

            // Sinhala Unicode range
            if (unicodeCharacter >= 0x0D80 && unicodeCharacter <= 0x0DFF) return true;

            // Thai Unicode range
            if (unicodeCharacter >= 0x0E00 && unicodeCharacter <= 0x0E7F) return true;

            // Lao Unicode range
            if (unicodeCharacter >= 0x0E80 && unicodeCharacter <= 0x0EFF) return true;

            // Tibetan Unicode range
            if (unicodeCharacter >= 0x0F00 && unicodeCharacter <= 0x0FFF) return true;

            // Myanmar Unicode range
            if (unicodeCharacter >= 0x1000 && unicodeCharacter <= 0x109F) return true;

            // Georgian Unicode range
            if (unicodeCharacter >= 0x10A0 && unicodeCharacter <= 0x10FF) return true;

            // Hangul Jamo (Korean) Unicode range
            if (unicodeCharacter >= 0x1100 && unicodeCharacter <= 0x11FF) return true;

            // Ethiopic Unicode range
            if (unicodeCharacter >= 0x1200 && unicodeCharacter <= 0x137F) return true;

            // Khmer Unicode range
            if (unicodeCharacter >= 0x1780 && unicodeCharacter <= 0x17FF) return true;

            // Mongolian Unicode range
            if (unicodeCharacter >= 0x1800 && unicodeCharacter <= 0x18AF) return true;


            return false;
        }

        public static bool NeedsRTL(string text)
        {
            // Check if the text is null or empty
            if (string.IsNullOrEmpty(text)) return false;

            // Iterate over each character in the text
            foreach (char c in text)
            {
                int unicodeCharacter = c;

                // Check if the character's script is traditionally RTL
                // Arabic, Hebrew, Syriac, Thaana, etc.
                if ((unicodeCharacter >= 0x0600 && unicodeCharacter <= 0x06FF) || // Arabic
                    (unicodeCharacter >= 0x0590 && unicodeCharacter <= 0x05FF) || // Hebrew
                    (unicodeCharacter >= 0x0700 && unicodeCharacter <= 0x074F) || // Syriac
                    (unicodeCharacter >= 0x0780 && unicodeCharacter <= 0x07BF) || // Thaana
                    (unicodeCharacter >= 0x0800 && unicodeCharacter <= 0x083F))   // Samaritan
                {
                    return true;
                }
            }

            // If no RTL characters found, return false
            return false;
        }


        protected DecomposedText DecomposeText(string text, SKPaint paint,
    SKPoint firstLineOffset,
    float maxWidth,
    float maxHeight,//-1
    int maxLines,//-1
    bool needsShaping,
    TextSpan span)
        {

            if (span != null)
            {
                needsShaping = span.NeedShape;
            }

            var ret = new DecomposedText();
            bool isCut = false;
            float totalHeight = 0;
            var countLines = 0;

            float lineMaxHeight = 0f;

            bool offsetFirstLine = false;
            var limitWidth = maxWidth;

            var paragraphs = text.Split('\n');
            ret.CountParagraphs = paragraphs.Length;

            var result = new List<TextLine>();

            foreach (var line in paragraphs)
            {
                bool isNewParagraph = firstLineOffset.X == 0; //really first

                countLines++;

                if (!offsetFirstLine)
                {
                    offsetFirstLine = true;
                    limitWidth = maxWidth - firstLineOffset.X;
                }
                else
                {
                    limitWidth = maxWidth;
                }

                if (maxLines > -1 && countLines > maxLines)
                {
                    isCut = true;
                    break;
                }

                var lineIndex = 0;
                var lineResult = "";
                float width = 0;
                var space = ' ';
                bool spanPostponed = false;

                Stack<string> stackWords = new Stack<string>(SplitLineToWords(line, space));

                void AddEmptyLine()
                {
                    bool assingnIsNewParagraph = isNewParagraph;
                    isNewParagraph = false; //have to set again to true upstairs

                    var widthBlock = 0;
                    var heightBlock = LineHeightPixels;

                    var chunk = new LineSpan()
                    {
                        NeedsShaping = needsShaping,
                        Glyphs = Array.Empty<LineGlyph>(),
                        Text = "",
                        Span = span,
                        Size = new(widthBlock, heightBlock)
                    };
                    var addLine = new TextLine()
                    {
                        Value = "",
                        IsNewParagraph = assingnIsNewParagraph,
                        Width = widthBlock,
                        Spans = new()
                        {
                            chunk
                        },
                        Height = heightBlock
                    };

                    if (addLine.Height > lineMaxHeight)
                        lineMaxHeight = addLine.Height;
                    result.Add(addLine);

                    if (assingnIsNewParagraph && result.Count > 1)
                    {
                        totalHeight += (float)SpaceBetweenParagraphs;
                    }
                    width = 0;
                    lineResult = "";
                    limitWidth = maxWidth;
                }

                //returns true if need stop processing: was last allowed line
                bool AddLine(string adding, string full = null)
                {
                    bool assingnIsNewParagraph = isNewParagraph;

                    isNewParagraph = false; //have to set again to true upstairs

                    bool retAdd = true;
                    var wasLastChunk = false;

                    totalHeight += (float)LineHeightWithSpacing;
                    limitWidth = maxWidth; //reset the first line offset

                    if ((maxHeight > -1 && maxHeight < totalHeight + LineHeightWithSpacing)
                        || (maxLines > -1 && maxLines == result.Count + 1))
                    {
                        wasLastChunk = true;
                        retAdd = false;
                    }

                    if (!string.IsNullOrEmpty(adding))
                    {

                        if (wasLastChunk)
                        {
                            if (!string.IsNullOrEmpty(full)) //we didn't fit
                            {

                                if (LineBreakMode == LineBreakMode.TailTruncation)
                                {
                                    var maybeTrail = full + Trail;
                                    var limitText = CutLineToFit(paint, maybeTrail, limitWidth);
                                    if (limitText.Limit > 0)
                                    {
                                        adding = maybeTrail.Left(limitText.Limit).TrimEnd() + Trail;
                                        width = limitText.Width;
                                    }
                                    else
                                    {
                                        adding = maybeTrail;
                                        width = limitText.Width;
                                    }

                                }

                                isCut = true;

                            }
                        }

                        var smartMeasure = MeasureLineGlyphs(paint, adding, needsShaping);

                        var widthBlock = (float)Math.Round(smartMeasure.Width);
                        var heightBlock = LineHeightPixels;

                        var chunk = new LineSpan()
                        {
                            NeedsShaping = needsShaping,
                            Glyphs = smartMeasure.Glyphs,
                            Text = adding.Replace("\n", ""),
                            Span = span,
                            Size = new(widthBlock, heightBlock)
                        };

                        var addLine = new TextLine()
                        {
                            Value = adding,
                            IsNewParagraph = assingnIsNewParagraph,
                            Width = widthBlock,
                            Height = heightBlock,
                            Spans = new()
                            {
                                chunk
                            }
                        };

                        if (result.Count > 0)
                        {
                            result[^1].IsLastInParagraph = addLine.IsNewParagraph;
                        }

                        if (addLine.Height > lineMaxHeight)
                            lineMaxHeight = addLine.Height;
                        result.Add(addLine);

                        if (assingnIsNewParagraph && result.Count > 1)
                        {
                            totalHeight += (float)SpaceBetweenParagraphs;
                        }

                        width = 0;
                        lineResult = "";
                    }

                    return retAdd;
                }

                void PostponeToNextLine(string text)
                {
                    stackWords.Push(text);
                    lineResult = "";
                    width = 0;
                }

                while (stackWords.Count > 0)
                {
                    var word = stackWords.Pop();

                    if (KeepSpacesOnLineBreaks && lineIndex > 0)
                    {
                        word += space;
                    }
                    lineIndex++;

                    var textLine = word;

                    bool severalWords = false;
                    if (width > 0) //got some text from previous pass
                    {
                        if (lineResult.Right(1) == " " || word.Left() == " ")
                        {
                            textLine = lineResult + word;
                        }
                        else
                        {
                            textLine = lineResult + space + word;
                        }
                        severalWords = true;
                    }

                    var textWidth = MeasureLineGlyphs(paint, textLine, needsShaping).Width;

                    //apply

                    width = textWidth;

                    if (width > limitWidth)
                    {
                        //the whole word is bigger than width, need break word,
                        if (severalWords)
                        {
                            //cannot add this word
                            if (!AddLine(lineResult, textLine))
                            {
                                break; //was last allowed line
                            }
                            PostponeToNextLine(word); //push word
                            continue;
                        }

                        if (result.Count == 0 && !spanPostponed && firstLineOffset.X > 0)
                        {
                            //not fitting new span, just postpone to next line
                            spanPostponed = true;

                            if (lineIndex == maxLines)
                            {
                                AddLine(word, textLine);
                                break;
                            }

                            AddEmptyLine();
                            PostponeToNextLine(word); //push word
                            continue;
                        }

                        var cycle = "";
                        var bounds = new SKRect();
                        var maybeLimit = 0;
                        var savedWidth = 0.0f;
                        int lenInsideWord = 0;
                        int posInsideWord = 0;
                        bool needBreak = false;
                        for (int pos = 0; pos < textLine.Length; pos++) //todo replace this with glyphs!!!
                        {
                            lenInsideWord++;
                            cycle = textLine.Substring(posInsideWord, lenInsideWord);// + Trail; todo
                            MeasureText(paint, cycle, ref bounds);
                            //posInsideWord = pos;
                            //posInsideWord = pos;
                            //lenInsideWord = 0;
                            if (bounds.Width > limitWidth)
                            {
                                //isCut = true;

                                //remove one last character to maybe fit?
                                var chunk = textLine.Substring(posInsideWord, lenInsideWord - 1);

                                width = MeasureLineGlyphs(paint, chunk, needsShaping).Width;

                                var pass = textLine;
                                if (paragraphs.Length > 1)
                                    pass = null;

                                if (maxLines > -1 && maxLines == result.Count + 1) //last allowed line
                                {
                                    isCut = true;
                                    AddLine(chunk, pass);
                                    needBreak = true;
                                    break;
                                }

                                var postpone = AddLine(chunk, pass);

                                if (postpone)
                                {
                                    var cut = textLine.Substring(posInsideWord + lenInsideWord - 1, textLine.Length - (lenInsideWord - 1));

                                    PostponeToNextLine(cut);
                                }
                                else
                                {
                                    needBreak = true;
                                }


                                break;
                            }
                            else
                            {
                                if (pos == textLine.Length - 1)
                                {
                                    //last character, add everything
                                    AddLine(textLine, textLine);
                                }
                            }

                        }

                        if (needBreak)
                        {
                            break;
                        }






                    }
                    else
                    {

                        lineResult = textLine;
                    }
                }

                //last line
                if (stackWords.Count == 0) //!string.IsNullOrEmpty(lineResult) &&
                {
                    if (string.IsNullOrEmpty(lineResult) && span != null)
                    {
                        AddEmptyLine(); //we can add an empty one because we gonna merge spans later and remove empty lines eventually
                    }
                    else
                        AddLine(lineResult);
                }

                if (isCut) // If the text is cut  break paragraphs loop
                {
                    break;
                }

            }

            //finished iterating paragraphs

            if (result.Count > 0)
            {
                result[^1].IsLastInParagraph = true;
            }

            ret.WasCut = isCut;
            ret.Lines = result.ToArray();

            if (maxHeight > 0 && !isCut)
            {
                ret.HasMoreVerticalSpace = (float)(maxHeight - (totalHeight + LineHeightWithSpacing));
            }
            if (result.Count > 0)
            {
                ret.HasMoreHorizontalSpace = limitWidth - ret.Lines.Max(x => x.Width); // ret.Lines.Max(x => x.Width) < maxWidth + 0.5;
            }

            return ret;
        }


        public override void Invalidate()
        {
            ResetTextCalculations(); //force recalc

            base.Invalidate();

            Update();
        }

        public override void CalculateMargins()
        {
            base.CalculateMargins();

            ResetTextCalculations();
        }

        protected static void NeedUpdateFont(BindableObject bindable, object oldvalue, object newvalue)
        {

            var control = bindable as SkiaLabel;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.UpdateFont();
                }
            }
        }

        protected override void OnLayoutReady()
        {
            base.OnLayoutReady();

            if (AutoSize != AutoSizeType.None)
                Invalidate();
        }

        public override void OnScaleChanged()
        {
            UpdateFont();
        }

        public override bool CanDraw
        {
            get
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return DrawWhenEmpty && base.CanDraw;
                }

                return base.CanDraw;
            }
        }


        #region PROPERTIES

        public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes),
        typeof(FontAttributes),
        typeof(SkiaLabel),
        FontAttributes.None,
        propertyChanged: NeedUpdateFont);

        [TypeConverter(typeof(FontAttributesConverter))]
        public FontAttributes FontAttributes
        {
            get { return (FontAttributes)GetValue(FontAttributesProperty); }
            set { SetValue(FontAttributesProperty, value); }
        }

        public static readonly BindableProperty DrawWhenEmptyProperty = BindableProperty.Create(nameof(Tag),
        typeof(bool),
        typeof(SkiaLabel),
        true, propertyChanged: NeedInvalidateMeasure);
        public bool DrawWhenEmpty
        {
            get { return (bool)GetValue(DrawWhenEmptyProperty); }
            set { SetValue(DrawWhenEmptyProperty, value); }
        }

        public static readonly BindableProperty KeepSpacesOnLineBreaksProperty = BindableProperty.Create(
            nameof(KeepSpacesOnLineBreaks),
            typeof(bool),
            typeof(SkiaLabel),
            false,
            propertyChanged: NeedInvalidateMeasure);

        public bool KeepSpacesOnLineBreaks
        {
            get { return (bool)GetValue(KeepSpacesOnLineBreaksProperty); }
            set { SetValue(KeepSpacesOnLineBreaksProperty, value); }
        }


        public static readonly BindableProperty FontWeightProperty = BindableProperty.Create(
            nameof(FontWeight),
            typeof(int),
            typeof(SkiaLabel),
            0, propertyChanged: NeedUpdateFont);

        public int FontWeight
        {
            get { return (int)GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }

        public static readonly BindableProperty TypeFaceProperty = BindableProperty.Create(
            nameof(TypeFace),
            typeof(SKTypeface),
            typeof(SkiaLabel),
            defaultValue: null,
            propertyChanged: NeedUpdateFont);

        public SKTypeface TypeFace
        {
            get { return (SKTypeface)GetValue(TypeFaceProperty); }
            set { SetValue(TypeFaceProperty, value); }
        }

        public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(DrawTextAlignment),
            typeof(SkiaLabel),
            defaultValue: DrawTextAlignment.Start,
            propertyChanged: NeedInvalidateMeasure);

        public DrawTextAlignment HorizontalTextAlignment
        {
            get { return (DrawTextAlignment)GetValue(HorizontalTextAlignmentProperty); }
            set { SetValue(HorizontalTextAlignmentProperty, value); }
        }

        public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaLabel),
            defaultValue: TextAlignment.Start,
            propertyChanged: NeedInvalidateMeasure);

        public TextAlignment VerticalTextAlignment
        {
            get { return (TextAlignment)GetValue(VerticalTextAlignmentProperty); }
            set { SetValue(VerticalTextAlignmentProperty, value); }
        }


        public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(
            nameof(LineHeight),
            typeof(double),
            typeof(SkiaLabel),
            1.0,
            propertyChanged: NeedUpdateFont);

        public double LineHeight
        {
            get { return (double)GetValue(LineHeightProperty); }
            set { SetValue(LineHeightProperty, value); }
        }


        public static readonly BindableProperty SensorRotationProperty = BindableProperty.Create(
            nameof(SensorRotation),
            typeof(double),
            typeof(SkiaLabel),
            0.0,
            propertyChanged: NeedDraw);

        public double SensorRotation
        {
            get { return (double)GetValue(SensorRotationProperty); }
            set { SetValue(SensorRotationProperty, value); }
        }

        public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaLabel),
            defaultValue: string.Empty,
            propertyChanged: NeedUpdateFont);

        public string FontFamily
        {
            get { return (string)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
            typeof(int), typeof(SkiaLabel), -1,
            propertyChanged: NeedInvalidateMeasure);
        public int MaxLines
        {
            get { return (int)GetValue(MaxLinesProperty); }
            set { SetValue(MaxLinesProperty, value); }
        }

        //public static readonly BindableProperty AllowUnicodeProperty = BindableProperty.Create(
        //    nameof(AllowUnicode),
        //    typeof(bool),
        //    typeof(SkiaLabel),
        //    true);

        //public bool AllowUnicode
        //{
        //    get { return (bool)GetValue(AllowUnicodeProperty); }
        //    set { SetValue(AllowUnicodeProperty, value); }
        //}


        public static readonly BindableProperty AutoSizeProperty = BindableProperty.Create(nameof(AutoSize),
            typeof(AutoSizeType), typeof(SkiaLabel),
            AutoSizeType.None,
            propertyChanged: NeedInvalidateMeasure);
        public AutoSizeType AutoSize
        {
            get { return (AutoSizeType)GetValue(AutoSizeProperty); }
            set { SetValue(AutoSizeProperty, value); }
        }

        public static readonly BindableProperty AutoSizeTextProperty = BindableProperty.Create(
            nameof(AutoSizeText),
            typeof(string),
            typeof(SkiaLabel),
            null,
            propertyChanged: NeedInvalidateMeasure);

        public string AutoSizeText
        {
            get { return (string)GetValue(AutoSizeTextProperty); }
            set { SetValue(AutoSizeTextProperty, value); }
        }

        public static readonly BindableProperty LineSpacingProperty = BindableProperty.Create(nameof(LineSpacing),
            typeof(double), typeof(SkiaLabel), 1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double LineSpacing
        {
            get { return (double)GetValue(LineSpacingProperty); }
            set { SetValue(LineSpacingProperty, value); }
        }

        public static readonly BindableProperty ParagraphSpacingProperty = BindableProperty.Create(nameof(ParagraphSpacing),
       typeof(double), typeof(SkiaLabel), 0.25,
       propertyChanged: NeedInvalidateMeasure);
        public double ParagraphSpacing
        {
            get { return (double)GetValue(ParagraphSpacingProperty); }
            set { SetValue(ParagraphSpacingProperty, value); }
        }

        public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing),
            typeof(double), typeof(SkiaLabel), 1.0,
            propertyChanged: NeedInvalidateMeasure);
        /// <summary>
        /// This applies ONLY when CharByChar is enabled
        /// </summary>
        public double CharacterSpacing
        {
            get { return (double)GetValue(CharacterSpacingProperty); }
            set { SetValue(CharacterSpacingProperty, value); }
        }

        public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(
            nameof(LineBreakMode),
            typeof(LineBreakMode),
            typeof(SkiaLabel),
            LineBreakMode.TailTruncation,
            propertyChanged: NeedInvalidateMeasure);

        public LineBreakMode LineBreakMode
        {
            get { return (LineBreakMode)GetValue(LineBreakModeProperty); }
            set { SetValue(LineBreakModeProperty, value); }
        }

        public static readonly BindableProperty FormattedTextProperty = BindableProperty.Create(
            nameof(FormattedText),
            typeof(FormattedString),
            typeof(SkiaLabel),
            defaultValue: null,
            propertyChanged: NeedInvalidateMeasure);

        public FormattedString FormattedText
        {
            get { return (FormattedString)GetValue(FormattedTextProperty); }
            set { SetValue(FormattedTextProperty, value); }
        }

        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(SkiaLabel),
            string.Empty,
            propertyChanged: TextWasChanged);

        private static void TextWasChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaLabel control)
            {
                control.OnTextChanged((string)newvalue);
            }
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        protected virtual void OnTextChanged(string value)
        {
            InvalidateMeasure();
        }

        public static readonly BindableProperty FallbackCharacterProperty = BindableProperty.Create(
            nameof(FallbackCharacter),
            typeof(char),
            typeof(SkiaLabel),
            ' ', propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Character to show when glyph is not found in font
        /// </summary>
        public char FallbackCharacter
        {
            get { return (char)GetValue(FallbackCharacterProperty); }
            set { SetValue(FallbackCharacterProperty, value); }
        }

        public static readonly BindableProperty MonoForDigitsProperty = BindableProperty.Create(
            nameof(MonoForDigits), typeof(string), typeof(SkiaLabel),
            string.Empty, propertyChanged: NeedInvalidateMeasure);

        public string MonoForDigits
        {
            get { return (string)GetValue(MonoForDigitsProperty); }
            set { SetValue(MonoForDigitsProperty, value); }
        }

        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
            nameof(TextColor), typeof(Color), typeof(SkiaLabel),
            Colors.GreenYellow,
            propertyChanged: NeedDraw);
        public Color TextColor
        {
            get { return (Color)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); }
        }

        public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
            nameof(StrokeColor),
            typeof(Color),
            typeof(SkiaLabel),
            Colors.Transparent,
            propertyChanged: NeedInvalidateMeasure);

        public Color StrokeColor
        {
            get { return (Color)GetValue(StrokeColorProperty); }
            set { SetValue(StrokeColorProperty, value); }
        }

        public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
            nameof(StrokeWidth),
            typeof(double),
            typeof(SkiaLabel),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

        public double StrokeWidth
        {
            get { return (double)GetValue(StrokeWidthProperty); }
            set { SetValue(StrokeWidthProperty, value); }
        }

        #region Drop Shadow

        public static readonly BindableProperty DropShadowColorProperty = BindableProperty.Create(
            nameof(DropShadowColor),
            typeof(Color),
            typeof(SkiaLabel),
            Colors.Transparent,
            propertyChanged: NeedDraw);

        public Color DropShadowColor
        {
            get { return (Color)GetValue(DropShadowColorProperty); }
            set { SetValue(DropShadowColorProperty, value); }
        }

        public static readonly BindableProperty DropShadowSizeProperty = BindableProperty.Create(
            nameof(DropShadowSize),
            typeof(double),
            typeof(SkiaLabel),
            2.0,
            propertyChanged: NeedDraw);

        public double DropShadowSize
        {
            get { return (double)GetValue(DropShadowSizeProperty); }
            set { SetValue(DropShadowSizeProperty, value); }
        }

        public static readonly BindableProperty DropShadowOffsetYProperty = BindableProperty.Create(
            nameof(DropShadowOffsetY),
            typeof(double),
            typeof(SkiaLabel),
            2.0,
            propertyChanged: NeedDraw);


        public double DropShadowOffsetY
        {
            get { return (double)GetValue(DropShadowOffsetYProperty); }
            set { SetValue(DropShadowOffsetYProperty, value); }
        }

        public static readonly BindableProperty DropShadowOffsetXProperty = BindableProperty.Create(
            nameof(DropShadowOffsetX),
            typeof(double),
            typeof(SkiaLabel),
            2.0,
            propertyChanged: NeedDraw);

        /// <summary>
        /// To make DropShadow act like shadow
        /// </summary>
        public double DropShadowOffsetX
        {
            get { return (double)GetValue(DropShadowOffsetXProperty); }
            set { SetValue(DropShadowOffsetXProperty, value); }
        }

        #endregion

        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(SkiaLabel),
            12.0,
            propertyChanged: NeedUpdateFont);

        public double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }



        //public static readonly BindableProperty RotateLayoutParametersProperty = BindableProperty.Create(
        //    nameof(RotateLayoutParameters),
        //    typeof(bool),
        //    typeof(SkiaLabel),
        //    false,
        //    propertyChanged: NeedInvalidateMeasure);

        //public bool RotateLayoutParameters
        //{
        //    get { return (bool)GetValue(RotateLayoutParametersProperty); }
        //    set { SetValue(RotateLayoutParametersProperty, value); }
        //}



        #region GRADIENT


        public static readonly BindableProperty GradientByLinesProperty = BindableProperty.Create(
            nameof(GradientByLines),
            typeof(bool),
            typeof(SkiaLabel),
            true,
            propertyChanged: NeedDraw);

        public bool GradientByLines
        {
            get { return (bool)GetValue(GradientByLinesProperty); }
            set { SetValue(GradientByLinesProperty, value); }
        }


        public static readonly BindableProperty StrokeGradientProperty = BindableProperty.Create(
            nameof(StrokeGradient),
            typeof(SkiaGradient),
            typeof(SkiaLabel),
            null,
            propertyChanged: StrokeGradientPropertyChanged);

        public SkiaGradient StrokeGradient
        {
            get { return (SkiaGradient)GetValue(StrokeGradientProperty); }
            set { SetValue(StrokeGradientProperty, value); }
        }


        private static void StrokeGradientPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
            {
                if (oldvalue is SkiaGradient skiaGradientOld)
                {
                    skiaGradientOld.Parent = null;
                    skiaGradientOld.BindingContext = null;
                }

                if (newvalue is SkiaGradient skiaGradient)
                {
                    skiaGradient.Parent = skiaControl;
                    skiaGradient.BindingContext = skiaControl.BindingContext;
                }

                skiaControl.Update();
            }

        }





        #endregion



        #endregion

        #region FAKE BOLD AND ITALIC

        protected virtual void ApplyFontProperties()
        {


        }

        protected virtual void SetItalic(SKPaint paint)
        {
            paint.TextSkewX = -0.25f; // Skew factor for faux italic; you can adjust this
        }

        protected virtual void SetBold(SKPaint paint)
        {
            paint.FakeBoldText = false;
        }

        protected virtual void SetDefault(SKPaint paint)
        {
            paint.TextSkewX = 0;
            paint.FakeBoldText = false;
        }


        #endregion

        #region GESTURES

        /// <summary>
        /// Return null if you wish not to consume
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public virtual ISkiaGestureListener OnSpanTapped(TextSpan span)
        {
            span.FireTap();
            return this;
        }

        public void OnFocusChanged(bool focus)
        { }


        public override ISkiaGestureListener ProcessGestures(TouchActionType type, TouchActionEventArgs args,
            TouchActionResult touchAction,
            SKPoint childOffset, SKPoint childOffsetDirect, ISkiaGestureListener alreadyConsumed)
        {
            if (touchAction == TouchActionResult.Tapped)
            {

                //apply transfroms
                var thisOffset = TranslateInputCoords(childOffset, true);
                //apply touch coords
                var x = args.Location.X + thisOffset.X;
                var y = args.Location.Y + thisOffset.Y;

                foreach (var span in Spans.ToList())
                {
                    if (span.HasTapHandler)
                    {
                        if (span.HitIsInside(x, y))
                        {
                            var ptsInsideControl = GetOffsetInsideControlInPoints(args.Location, childOffset);
                            PlayRippleAnimation(TouchEffectColor, ptsInsideControl.X, ptsInsideControl.Y);

                            return OnSpanTapped(span);
                        }
                    }
                }
            }

            return base.ProcessGestures(type, args, touchAction, childOffset, childOffsetDirect, alreadyConsumed);
        }


        #endregion

        public static readonly BindableProperty TouchEffectColorProperty = BindableProperty.Create(nameof(TouchEffectColor), typeof(Color),
            typeof(SkiaLabel),
            Colors.White);

        private SKTypeface _replaceFont;

        public Color TouchEffectColor
        {
            get { return (Color)GetValue(TouchEffectColorProperty); }
            set { SetValue(TouchEffectColorProperty, value); }
        }

        #region CACHE

        public static ConcurrentDictionary<GlyphSizeDefinition, ConcurrentDictionary<string, float>> MeasuredGlyphsWidthWithAdvance { get; } = new();


        public struct GlyphSizeDefinition
        {
            public SKTypeface Typeface { get; set; }
            public float FontSize { get; set; }

            public static bool operator ==(GlyphSizeDefinition a, GlyphSizeDefinition b)
            {
                return a.FontSize == b.FontSize && a.Typeface == b.Typeface;
            }

            public static bool operator !=(GlyphSizeDefinition a, GlyphSizeDefinition b)
            {
                return a.FontSize != b.FontSize || a.Typeface != b.Typeface;
            }
        }

        #endregion
    }


}
