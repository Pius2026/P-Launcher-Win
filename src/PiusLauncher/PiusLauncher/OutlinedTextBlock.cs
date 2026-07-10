using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PiusLauncher;

public sealed class OutlinedTextBlock : FrameworkElement
{
	public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FillProperty = DependencyProperty.Register("Fill", typeof(Brush), typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register("Stroke", typeof(Brush), typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty GlowStrokeProperty = DependencyProperty.Register("GlowStroke", typeof(Brush), typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)1.4, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty GlowThicknessProperty = DependencyProperty.Register("GlowThickness", typeof(double), typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)3.2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)48.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(OutlinedTextBlock), (PropertyMetadata)new FrameworkPropertyMetadata((object)FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public string Text
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TextProperty, (object)value);
		}
	}

	public Brush Fill
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(FillProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FillProperty, (object)value);
		}
	}

	public Brush Stroke
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(StrokeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(StrokeProperty, (object)value);
		}
	}

	public Brush GlowStroke
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(GlowStrokeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(GlowStrokeProperty, (object)value);
		}
	}

	public double StrokeThickness
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(StrokeThicknessProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(StrokeThicknessProperty, (object)value);
		}
	}

	public double GlowThickness
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(GlowThicknessProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(GlowThicknessProperty, (object)value);
		}
	}

	public FontFamily FontFamily
	{
		get
		{
			return (FontFamily)((DependencyObject)this).GetValue(FontFamilyProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FontFamilyProperty, (object)value);
		}
	}

	public double FontSize
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(FontSizeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FontSizeProperty, (object)value);
		}
	}

	public FontWeight FontWeight
	{
		get
		{
			return (FontWeight)((DependencyObject)this).GetValue(FontWeightProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FontWeightProperty, (object)value);
		}
	}

	private double OutlinePadding => Math.Max(0.0, StrokeThickness) + Math.Max(0.0, GlowThickness) / 2.0;

	protected override Size MeasureOverride(Size availableSize)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		FormattedText formattedText = CreateText(double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width);
		double outlinePadding = OutlinePadding;
		return new Size(Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace + outlinePadding * 2.0), Math.Ceiling(formattedText.Height + outlinePadding * 2.0));
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		base.OnRender(drawingContext);
		if (!string.IsNullOrWhiteSpace(Text))
		{
			double outlinePadding = OutlinePadding;
			Geometry geometry = CreateText(Math.Max(1.0, base.ActualWidth - outlinePadding * 2.0)).BuildGeometry(new Point(outlinePadding, outlinePadding));
			if (GlowThickness > 0.0)
			{
				drawingContext.DrawGeometry(null, new Pen(GlowStroke, GlowThickness)
				{
					LineJoin = PenLineJoin.Round
				}, geometry);
			}
			if (StrokeThickness > 0.0)
			{
				drawingContext.DrawGeometry(null, new Pen(Stroke, StrokeThickness)
				{
					LineJoin = PenLineJoin.Round
				}, geometry);
			}
			drawingContext.DrawGeometry(Fill, null, geometry);
		}
	}

	private FormattedText CreateText(double maxWidth)
	{
		FormattedText formattedText = new FormattedText(Text ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal), FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
		{
			Trimming = TextTrimming.CharacterEllipsis
		};
		if (!double.IsInfinity(maxWidth))
		{
			formattedText.MaxTextWidth = Math.Max(1.0, maxWidth - OutlinePadding * 2.0);
		}
		return formattedText;
	}
}
