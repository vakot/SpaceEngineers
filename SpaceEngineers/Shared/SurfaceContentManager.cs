#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace SpaceEngineers.Shared.SurfaceContentManager
{
    #endregion Prelude

    #region SurfaceContentManagerClass
    public class SurfaceContentManager
    {
        private MyGridProgram _Program;

        MyIni _ini = new MyIni();
        public const string IniSectionSurfaceGeneral = "Surface Manager - General",
                            IniKeyLCDTag = "LCD name tag";

        public string LCDTag { get; private set; } = "[LCD]";

        private List<SurfaceProvider> _Providers = new List<SurfaceProvider>();
        private static Dictionary<string, Action<SurfaceManager>> _ContentTypes = new Dictionary<string, Action<SurfaceManager>>();

        public SurfaceContentManager(MyGridProgram Program)
        {
            _Program = Program;

            Update();
        }

        public void Update()
        {
            ParseIni();

            // remove provider's that don't contain's [LCDTag] or isn't valid
            _Providers = _Providers.Where(x => x.CustomName.Contains(LCDTag) && IsValid(x)).ToList();

            List<IMyTextSurfaceProvider> Providers = new List<IMyTextSurfaceProvider>();
            _Program.GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(
                Providers,
                x => (x as IMyTerminalBlock).IsSameConstructAs(_Program.Me)
                && (x as IMyTerminalBlock).CustomName.Contains(LCDTag)
                && x.SurfaceCount > 0
            );

            // add new provider's
            foreach (IMyTextSurfaceProvider Provider in Providers) if (!IsExist(Provider)) _Providers.Add(new SurfaceProvider(Provider));

            // update provider's content
            foreach (SurfaceProvider _Provider in _Providers) _Provider.Update();
        }

        private void ParseIni()
        {
            _ini.Clear();
            if (_ini.TryParse(_Program.Me.CustomData))
            {
                LCDTag = _ini.Get(IniSectionSurfaceGeneral, IniKeyLCDTag).ToString(LCDTag);
            }
            else if (!string.IsNullOrWhiteSpace(_Program.Me.CustomData))
            {
                _ini.EndContent = _Program.Me.CustomData;
            }

            _ini.Set(IniSectionSurfaceGeneral, IniKeyLCDTag, LCDTag);

            string Output = _ini.ToString();
            if (Output != _Program.Me.CustomData)
            {
                _Program.Me.CustomData = Output;
            }
        }

        #region Draw
        public void DrawContent(int PixelsToScroll = 0, bool IsScroll = false)
        {
            foreach (SurfaceProvider _Provider in _Providers)
            {
                _Provider.DrawContent(PixelsToScroll, IsScroll);
            }
        }
        #endregion Draw

        #region Helpers
        public void AddContentType(string tag, Action<SurfaceManager> action)
        {
            try
            {
                _ContentTypes.Add(tag, action);
            }
            catch { return; }
        }

        private bool IsExist(IMyTextSurfaceProvider Provider)
        {
            int Hash = Provider.GetHashCode();
            foreach (SurfaceProvider _Provider in _Providers)
            {
                if (Hash == _Provider.GetHashCode()) return true;
            }
            return false;
        }
        private bool IsValid(SurfaceProvider Provider)
        {
            return Provider != null && !Provider.Closed;
        }

        public int SurfacesCount()
        {
            int Count = 0;
            foreach (SurfaceProvider _Provider in _Providers)
            {
                Count += _Provider.SurfaceCount;
            }
            return Count;
        }
        #endregion Helpers

        #region SurfaceProviderClass
        public class SurfaceProvider
        {
            private MyIni _ini = new MyIni();
            public const string IniSectionLCD = "Screen",
                                IniKeyContentType = "Content type",
                                IniKeyBackgroundColor = "Background color",
                                IniKeyDefaultColor = "Default color";

            private IMyTextSurfaceProvider _Provider;

            private List<SurfaceManager> _Surfaces = new List<SurfaceManager>();
            private List<string[]> _Contents = new List<string[]>();

            public string CustomName => (_Provider as IMyTerminalBlock).CustomName;
            public int SurfaceCount => _Provider.SurfaceCount;
            public override int GetHashCode() => _Provider.GetHashCode();
            public bool Closed => (_Provider as IMyTerminalBlock).Closed;

            public SurfaceProvider(IMyTextSurfaceProvider Provider)
            {
                _Provider = Provider;
            }

            public void Update()
            {
                for (int i = 0; i < _Provider.SurfaceCount; i++)
                {
                    ParseIni(i);
                }
            }

            private void ParseIni(int i = 0)
            {
                IMyTerminalBlock Block = _Provider as IMyTerminalBlock;
                SurfaceManager Manager = new SurfaceManager(_Provider.GetSurface(i));

                _ini.Clear();

                string backgroundColor = Manager.BackgroundColor.ToString();
                string defaultColor = Manager.DefaultColor.ToString();

                string[] Content = new string[] { "none" };

                if (_ini.TryParse(Block.CustomData))
                {
                    Content = _ini.Get($"{IniSectionLCD} ({i})", IniKeyContentType).ToString("none").Split(',');
                    backgroundColor = _ini.Get($"{IniSectionLCD} ({i})", IniKeyBackgroundColor).ToString(backgroundColor);
                    defaultColor = _ini.Get($"{IniSectionLCD} ({i})", IniKeyDefaultColor).ToString(defaultColor);
                }
                else if (!string.IsNullOrWhiteSpace(Block.CustomData))
                {
                    _ini.EndContent = Block.CustomData;
                }

                _ini.Set($"{IniSectionLCD} ({i})", IniKeyContentType, string.Join(",", Content));
                _ini.Set($"{IniSectionLCD} ({i})", IniKeyBackgroundColor, backgroundColor);
                _ini.Set($"{IniSectionLCD} ({i})", IniKeyDefaultColor, defaultColor);

                string Output = _ini.ToString();
                if (Output != Block.CustomData)
                {
                    Block.CustomData = Output;
                }

                if (Content.Length == 1 && Content[0] == "none") return;

                if (!IsExist(Manager))
                {
                    Manager.SetColors(TryParseColor(backgroundColor), TryParseColor(defaultColor));
                    _Surfaces.Add(Manager);
                    _Contents.Add(Content);
                }
                else
                {
                    int index = IndexOf(Manager);
                    if (index == -1) return;

                    if (!IsEquals(Content, _Contents[index])) _Surfaces[index].Reset();
                    _Surfaces[index].SetColors(TryParseColor(backgroundColor), TryParseColor(defaultColor));
                    _Contents[index] = Content;
                }
            }

            #region Helpers
            private Color TryParseColor(string Str)
            {
                try
                {
                    if (Str[0] != '{' || Str[Str.Length - 1] != '}') throw new Exception();

                    string[] Split = Str.Substring(1, Str.Length - 2).Split(' ');
                    if (Split.Length != 4) throw new Exception();

                    int[] RGBA = new int[] { 0, 0, 0, 255 };
                    for (int i = 0; i < Split.Length; i++)
                    {
                        RGBA[i] = int.Parse(Split[i].Substring(2, Split[i].Length - 2));
                    }

                    return new Color(RGBA[0], RGBA[1], RGBA[2], RGBA[3]);
                }
                catch { return Color.Transparent; }
            }
            private bool IsExist(SurfaceManager Manager)
            {
                int Hash = Manager.GetHashCode();

                foreach (SurfaceManager manager in _Surfaces)
                {
                    if (manager.GetHashCode() == Hash) return true;
                }

                return false;
            }

            private int IndexOf(SurfaceManager Manager)
            {
                for (int i = 0; i < _Surfaces.Count; i++)
                {
                    if (Manager.GetHashCode() == _Surfaces[i].GetHashCode()) return i;
                }

                return -1;
            }

            private bool IsEquals(string[] Array1, string[] Array2)
            {
                if (Array1.Length != Array2.Length) return false;

                for (int i = 0; i < Array1.Length; i++)
                {
                    if (Array1[i] != Array2[i]) return false;
                }

                return true;
            }
            #endregion Helpers

            #region Draw
            public void DrawContent(int PixelsToScroll, bool IsScroll)
            {
                for (int i = 0; i < _Surfaces.Count && i < _Contents.Count; i++)
                {
                    SurfaceManager Manager = _Surfaces[i];
                    string[] Content = _Contents[i];

                    try
                    {
                        Manager.Clear();
                        foreach (string Type in Content)
                        {
                            _ContentTypes[Type](Manager);
                            Manager.SaveLine();
                        }
                        Manager.Render(IsScroll ? PixelsToScroll : 0);
                    }
                    catch (Exception exception)
                    {
                        DrawBSOD(Manager, exception);
                    }

                }
            }

            private void DrawBSOD(SurfaceManager Manager, Exception exception)
            {
                Manager.Clear();

                Manager.AddTextBuilder(":(", new Vector2(0f, 0f), new Vector2(1f, 0.25f), Alignment: TextAlignment.LEFT, FontSize: 6f);

                Manager.AddTextBuilder(exception.Message, new Vector2(0f, 0.25f), new Vector2(1f, 0.9f), Alignment: TextAlignment.LEFT, FontSize: 1.1f, Multiline: true);

                Manager.AddTextBuilder("Please type correct setting's to Custom Data", new Vector2(0f, 0.9f), new Vector2(1f, 1f), Alignment: TextAlignment.LEFT, FontSize: 0.8f);

                Manager.Render(0);
            }
            #endregion Draw
        }
        #endregion SurfaceProviderClass

        #region SurfaceManagerClass
        public class SurfaceManager
        {
            private readonly IMyTextSurface _Surface;

            private readonly RectangleF _Viewport;
            private readonly Vector2 _Padding;
            private readonly float _Scale;

            private List<MySprite> _Sprites = new List<MySprite>();
            private List<List<MySprite>> _Lines = new List<List<MySprite>>();

            private int _ScrollDirection = -6;
            private float _ScrollValue = 0f;

            public Color BackgroundColor { get; private set; } = new Color(0, 88, 151);
            public Color DefaultColor { get; private set; } = new Color(179, 237, 255);

            public override int GetHashCode() => _Surface.GetHashCode();

            public SurfaceManager(IMyTextSurface Surface)
            {
                _Surface = Surface;
                _Viewport = new RectangleF((Surface.TextureSize - Surface.SurfaceSize) * 0.5f, Surface.SurfaceSize);

                Vector2 VScale = _Viewport.Size / 512f;
                _Scale = Math.Min(VScale.X, VScale.Y);

                _Padding = new Vector2(10f, 10f) * _Scale;

                _Viewport.Size -= _Padding * 4f;
                _Viewport.Position += _Padding * 2f;
            }

            #region Builders
            public void AddTextBuilder(
                string Text,
                Vector2 TopLeftCorner,
                Vector2 BottomRightCorner,
                Color? color = null,
                TextAlignment Alignment = TextAlignment.CENTER,
                bool ExtraPadding = false,
                bool Multiline = false,
                float FontSize = 1f
            )
            {
                if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

                FontSize *= _Scale;

                Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
                Vector2 ContentSize = BlockSize - _Padding - (ExtraPadding ? _Padding.X : 0);
                Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

                // Fix Size
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

                if (Alignment == TextAlignment.RIGHT) Position.X += ContentSize.X * 0.5f - (ExtraPadding ? _Padding.X : 0);
                if (Alignment == TextAlignment.LEFT) Position.X -= ContentSize.X * 0.5f - (ExtraPadding ? _Padding.X : 0);

                string _Text = "";
                if (Multiline)
                {
                    int j = 0;
                    for (int i = 0; i < Text.Length; i++)
                    {
                        Vector2 textSize = _Surface.MeasureStringInPixels(new StringBuilder(Text.Substring(j, i - j)), "Debug", FontSize);
                        if (textSize.X > ContentSize.X - _Padding.X * 2f)
                        {
                            _Text += "\n";
                            j = i;
                        }
                        _Text += Text[i];
                    }
                }
                else
                {
                    _Text = Text;

                    Vector2 textSize = _Surface.MeasureStringInPixels(new StringBuilder(_Text), "Debug", FontSize);
                    while (textSize.X >= ContentSize.X - _Padding.X * 2f)
                    {
                        _Text = _Text.Remove(_Text.Length - 1);
                        textSize = _Surface.MeasureStringInPixels(new StringBuilder(_Text), "Debug", FontSize);
                    }
                }


                Vector2 TextSize = _Surface.MeasureStringInPixels(new StringBuilder(_Text), "Debug", FontSize);
                Position = new Vector2(Position.X, Position.Y - TextSize.Y * 0.5f);

                _Sprites.Add(new MySprite(SpriteType.TEXT, _Text, Position, ContentSize - _Padding * 2f, color ?? DefaultColor, "Debug", Alignment, FontSize));
            }
            public void AddCircleProgressBarBuilder(
                float Percentage,
                float Size,
                Vector2 TopLeftCorner,
                Vector2 BottomRightCorner,
                float Sector = 270,
                float Rotation = 0,
                int Cells = 1,
                Color? color = null,
                bool Reverse = false
            )
            {
                if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

                Percentage = Math.Max(Math.Min(Percentage, 1f), 0f);

                Color _Color = color ?? DefaultColor;
                Color _GhostColor = new Color(_Color, 0.1f);

                Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
                Vector2 ContentSize = BlockSize - _Padding;
                Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

                float CircleSize = Math.Min(ContentSize.X, ContentSize.Y) - 2f * Math.Min(_Padding.X, _Padding.Y);
                float Radius = CircleSize * 0.5f;

                // Fix Size
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

                Vector2 Offset = new Vector2(0f, 0f);
                float SeparatorWidth = 2f * (float)Math.PI * Radius / 180;

                // Unfilled
                for (int i = 0; i <= Sector; i++)
                {
                    float Angle = Sector - i;
                    if (Reverse) Angle = Sector - Angle;

                    Offset = new Vector2(
                        -(float)Math.Cos(MathHelper.ToRadians(Angle + Rotation)) * Radius,
                        -(float)Math.Sin(MathHelper.ToRadians(Angle + Rotation)) * Radius
                    );

                    DrawLine((Position + Offset * (1 - Size)), Position + Offset, _GhostColor, SeparatorWidth);
                }

                // Filled
                for (int i = 0; i <= Sector * Percentage; i++)
                {
                    float Angle = Sector - i;
                    if (Reverse) Angle = Sector - Angle;

                    Offset = new Vector2(
                        -(float)Math.Cos(MathHelper.ToRadians(Angle + Rotation)) * Radius,
                        -(float)Math.Sin(MathHelper.ToRadians(Angle + Rotation)) * Radius
                    );

                    DrawLine((Position + Offset * (1 - Size)), Position + Offset, _Color, SeparatorWidth);
                }

                if (Cells <= 1) return;

                // Cells
                for (int i = 0; i < Cells; i++)
                {
                    float Angle = Sector / Cells * i;
                    if (!Reverse) Angle = Sector - Angle;

                    Offset = new Vector2(
                        -(float)Math.Cos(MathHelper.ToRadians(Angle + Rotation)) * Radius,
                        -(float)Math.Sin(MathHelper.ToRadians(Angle + Rotation)) * Radius
                    );

                    DrawLine((Position + Offset * (1 - Size)), Position + Offset, BackgroundColor, SeparatorWidth);
                }
            }
            public void AddSquareProgressBarBuilder(
                float Percentage,
                Vector2 TopLeftCorner,
                Vector2 BottomRightCorner,
                int Rotation = 0,
                int Cells = 1,
                Color? color = null
            )
            {
                if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

                Percentage = Math.Max(Math.Min(Percentage, 1f), 0f);

                Color _Color = color ?? DefaultColor;
                Color _GhostColor = new Color(_Color, 0.1f);

                Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
                Vector2 ContentSize = BlockSize - _Padding;
                Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

                // Fix Size
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

                Vector2 BarSize = ContentSize - _Padding * 2f;
                Vector2 ActiveSize, BarPosition;

                Vector2 StartPos, EndPos, SeparatorSize;

                switch (Rotation)
                {
                    case 90:
                        ActiveSize = BarSize * new Vector2(Percentage, 1f);
                        BarPosition = new Vector2(Position.X + (ContentSize.X - ActiveSize.X) * 0.5f - _Padding.X, Position.Y);
                        StartPos = new Vector2(Position.X - ContentSize.X * 0.5f + _Padding.Y * 0.5f, Position.Y);
                        EndPos = new Vector2(Position.X + ContentSize.X * 0.5f - _Padding.Y * 0.5f, Position.Y);
                        SeparatorSize = new Vector2(Math.Min(_Padding.X, _Padding.Y) * 0.5f, BarSize.Y);
                        break;

                    case 180:
                        ActiveSize = BarSize * new Vector2(1f, Percentage);
                        BarPosition = new Vector2(Position.X, Position.Y + (ActiveSize.Y - ContentSize.Y) * 0.5f + _Padding.Y);
                        StartPos = new Vector2(Position.X, Position.Y - ContentSize.Y * 0.5f + _Padding.Y * 0.5f);
                        EndPos = new Vector2(Position.X, Position.Y + ContentSize.Y * 0.5f - _Padding.Y * 0.5f);
                        SeparatorSize = new Vector2(BarSize.X, Math.Min(_Padding.X, _Padding.Y) * 0.5f);
                        break;

                    case 270:
                        ActiveSize = BarSize * new Vector2(Percentage, 1f);
                        BarPosition = new Vector2(Position.X + (ActiveSize.X - ContentSize.X) * 0.5f + _Padding.X, Position.Y);
                        StartPos = new Vector2(Position.X - ContentSize.X * 0.5f + _Padding.Y * 0.5f, Position.Y);
                        EndPos = new Vector2(Position.X + ContentSize.X * 0.5f - _Padding.Y * 0.5f, Position.Y);
                        SeparatorSize = new Vector2(Math.Min(_Padding.X, _Padding.Y) * 0.5f, BarSize.Y);
                        break;

                    default:
                        ActiveSize = BarSize * new Vector2(1f, Percentage);
                        BarPosition = new Vector2(Position.X, Position.Y + (ContentSize.Y - ActiveSize.Y) * 0.5f - _Padding.Y);
                        StartPos = new Vector2(Position.X, Position.Y - ContentSize.Y * 0.5f + _Padding.Y * 0.5f);
                        EndPos = new Vector2(Position.X, Position.Y + ContentSize.Y * 0.5f - _Padding.Y * 0.5f);
                        SeparatorSize = new Vector2(BarSize.X, Math.Min(_Padding.X, _Padding.Y) * 0.5f);
                        break;
                }

                // Unfilled
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BarSize, _GhostColor));
                // Body
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", BarPosition, ActiveSize, _Color));

                if (Cells <= 1) return;

                Vector2 Offset = (EndPos - StartPos) / Cells;
                for (int i = 1; i < Cells; i++)
                {
                    Vector2 SeparatorPosition = StartPos + Offset * i;
                    _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", SeparatorPosition, SeparatorSize, BackgroundColor));
                }
            }
            public void AddGraphBuilder(
                List<float> Values,
                Vector2 TopLeftCorner,
                Vector2 BottomRightCorner,
                Color? color = null,
                bool DisplayPercentage = true,
                bool Filled = false
            )
            {
                if (Values.Count <= 0) return;
                if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

                float FontSize = 1.25f * _Scale;

                Color _Color = color ?? DefaultColor;

                Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
                Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

                // Fix Size
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

                Vector2 ContentSize = BlockSize - _Padding;
                Vector2 GraphBoxSize = ContentSize - _Padding;

                Vector2 TextSize = new Vector2(0f, 0f);
                if (DisplayPercentage)
                {
                    TextSize = _Surface.MeasureStringInPixels(new StringBuilder("000.0%"), "Debug", FontSize);
                    TextSize.X += _Padding.X;
                }
                GraphBoxSize -= TextSize;

                float Offset = GraphBoxSize.X / (Values.Count - 1);
                float Value = Math.Max(Math.Min(Values[0], 1), 0);
                Vector2 ZeroPoint = new Vector2(Position.X - (GraphBoxSize.X + TextSize.X) * 0.5f, Position.Y + GraphBoxSize.Y * 0.5f);
                Vector2 StartPoint = new Vector2(ZeroPoint.X, ZeroPoint.Y - GraphBoxSize.Y * Value);

                float Size = Math.Max(_Padding.X, _Padding.Y) * 0.5f;

                // Graph
                for (int i = 1; i < Values.Count; i++)
                {
                    Value = Math.Max(Math.Min(Values[i], 1), 0);
                    Vector2 EndPoint = new Vector2(ZeroPoint.X + i * Offset, ZeroPoint.Y - GraphBoxSize.Y * Value);

                    DrawLine(StartPoint, EndPoint, _Color, Size);
                    if (i == 1) _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", StartPoint, new Vector2(Size, Size), _Color));
                    _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", EndPoint, new Vector2(Size, Size), _Color));

                    // Fill
                    if (Filled)
                    {
                        Vector2 Difference = EndPoint - StartPoint;

                        float X = StartPoint.X;
                        while (X <= EndPoint.X + _Scale)
                        {
                            float Y = (X - StartPoint.X) / Difference.X * Difference.Y;
                            DrawLine(new Vector2(X, ZeroPoint.Y), new Vector2(X, StartPoint.Y + Y), _Color, Size * 0.5f);
                            X++;
                        }
                    }

                    StartPoint = EndPoint;
                }
                // Fill smooth bottom
                if (Filled)
                {
                    Vector2 Start = ZeroPoint;
                    Vector2 End = new Vector2(ZeroPoint.X + GraphBoxSize.X, ZeroPoint.Y);

                    DrawLine(Start, End, _Color, Size);
                    _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", Start, new Vector2(Size, Size), _Color));
                    _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", End, new Vector2(Size, Size), _Color));
                }
                // Running Percentage 
                if (DisplayPercentage) _Sprites.Add(new MySprite(
                    SpriteType.TEXT,
                    String.Format("{0:0.0}%", Values[Values.Count - 1] * 100f),
                    new Vector2(StartPoint.X + TextSize.X - _Padding.X * 0.25f, StartPoint.Y - TextSize.Y * 0.5f),
                    null,
                    _Color,
                    "Debug",
                    TextAlignment.RIGHT,
                    FontSize
                ));
            }
            public void AddBorderBuilder(
                Vector2 TopLeftCorner,
                Vector2 BottomRightCorner,
                Vector2? Gaps = null,
                Color? color = null
            )
            {
                if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

                Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
                Vector2 ContentSize = BlockSize - _Padding;
                Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

                // Fix Size
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

                // Border
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, ContentSize, color ?? DefaultColor));

                if (Gaps != null)
                {
                    // Vertical Gaps
                    _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, new Vector2(ContentSize.X, Gaps.Value.Y * ContentSize.Y), BackgroundColor));
                    // Horizontal Gaps
                    _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, new Vector2(Gaps.Value.X * ContentSize.X, ContentSize.Y), BackgroundColor));
                }

                // Inner
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, ContentSize - _Padding * 0.75f, BackgroundColor));
            }
            public void AddSpriteBuilder(
                string Type,
                Vector2 TopLeftCorner,
                Vector2 BottomRightCorner,
                Color? color = null,
                TextAlignment Alignment = TextAlignment.CENTER,
                bool KeepAspectRatio = true
            )
            {
                if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

                Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
                Vector2 ContentSize = BlockSize - _Padding;
                Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

                // Fix Size
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

                float Size = Math.Min(ContentSize.X, ContentSize.Y);

                if (Alignment == TextAlignment.RIGHT) Position.X += (ContentSize.X - Size) * 0.5f;
                if (Alignment == TextAlignment.LEFT) Position.X -= (ContentSize.X - Size) * 0.5f;

                if (KeepAspectRatio) ContentSize = new Vector2(Size, Size);

                _Sprites.Add(new MySprite(SpriteType.TEXTURE, Type, Position, ContentSize - _Padding * 2f, color ?? DefaultColor));
            }
            #endregion Builders

            #region Changers
            public void SetColors(Color backgroundColor, Color defaultColor)
            {
                BackgroundColor = backgroundColor;
                DefaultColor = defaultColor;
            }
            public void Reset()
            {
                _ScrollDirection = 1;
                _ScrollValue = 0f;
            }
            public void SaveLine()
            {
                if (_Sprites.Count <= 0) return;

                _Lines.Add(new List<MySprite>(_Sprites));
                _Sprites.Clear();
            }
            public void Clear()
            {
                _Lines.Clear();
                _Sprites.Clear();
            }
            private void SetupDrawSurface()
            {
                _Surface.ContentType = ContentType.SCRIPT;
                _Surface.Script = "";
                _Surface.ScriptBackgroundColor = BackgroundColor;
            }
            #endregion Changers

            #region Render
            public void Render(int PixelsToScroll = 0)
            {
                if (_Sprites.Count > 0) SaveLine();

                SetupDrawSurface();

                if (PixelsToScroll > 0) RunScroll(PixelsToScroll);

                MySpriteDrawFrame Frame = _Surface.DrawFrame();

                float Offset = 0f;
                for (int i = 0; i < _Lines.Count; i++)
                {
                    DrawSprites(ref Frame, _Lines[i], (Offset - _ScrollValue));
                    Offset += GetLineHeight(_Lines[i]);
                }
                Frame.Dispose();
            }
            private void DrawSprites(ref MySpriteDrawFrame Frame, List<MySprite> Sprites, float Offset)
            {
                foreach (MySprite Sprite in Sprites)
                {
                    MySprite sprite = Sprite;
                    sprite.Position += new Vector2(0f, Offset);
                    Frame.Add(sprite);
                }
            }
            private void RunScroll(int Offset)
            {
                float FrameHeight = GetFrameHeight();

                if (FrameHeight > _Viewport.Size.Y)
                {
                    float LowerLimit = 0f;
                    float UpperLimit = FrameHeight - _Viewport.Size.Y;

                    _ScrollValue = Math.Max(Math.Min(
                        _ScrollValue + Offset * _Scale * (_ScrollDirection > 0 ? 1 : (_ScrollDirection < 0 ? -1 : 0)),
                    UpperLimit), LowerLimit);

                    if (_ScrollValue <= LowerLimit && _ScrollDirection <= 0) _ScrollDirection++;
                    else if (_ScrollValue >= UpperLimit && _ScrollDirection >= 0) _ScrollDirection--;

                    if (_ScrollDirection < 0 && !(_ScrollValue <= LowerLimit)) _ScrollDirection = -6;
                    if (_ScrollDirection > 0 && !(_ScrollValue >= UpperLimit)) _ScrollDirection = 6;
                }
            }
            #endregion Render

            #region Helpers
            private void DrawLine(Vector2 Point1, Vector2 Point2, Color color, float Width)
            {
                Vector2 Position = 0.5f * (Point1 + Point2);
                Vector2 Difference = (Point1 - Point2);

                float Length = Difference.Length();
                if (Length != 0)
                    Difference /= Length;

                Vector2 Size = new Vector2(Length, Width);

                float Angle = (float)Math.Acos(Vector2.Dot(Difference, Vector2.UnitX));
                Angle *= Math.Sign(Vector2.Dot(Difference, Vector2.UnitY));

                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, Size, color, null, TextAlignment.CENTER, Angle));
            }
            private float GetLineHeight(List<MySprite> Line)
            {
                float LineHeight = 0;
                foreach (MySprite Sprite in Line)
                {
                    if (Sprite.Size != null && Sprite.Position != null)
                    {
                        LineHeight = Math.Max(Sprite.Position.Value.Y + Sprite.Size.Value.Y * 0.5f, LineHeight);
                    }
                }
                return LineHeight - _Viewport.Position.Y;
            }
            private float GetFrameHeight()
            {
                float TotalLinesHeight = 0f;
                foreach (List<MySprite> Line in _Lines)
                {
                    TotalLinesHeight += GetLineHeight(Line);
                }
                return TotalLinesHeight;
            }
            #endregion Helpers
        }
        #endregion SurfaceManagerClass
    }
    #endregion SurfaceContentManagerClass

#region PreludeFooter
}
#endregion PreludeFooter