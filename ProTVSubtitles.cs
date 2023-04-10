
using System;
using ArchiTech;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ProTVSubtitles : UdonSharpBehaviour
{
    [Tooltip("Drag the ProTV prefab containing the TVManagerV2 script here")]
    public TVManagerV2 tvManager;
    [Tooltip("Enable or disable subtitles, you should make a toggle for this")]
    public bool subtitlesEnabled = true;
    [Tooltip("The text object that will display the subtitles")]
    public TextMeshProUGUI subtitleTextObj;
    
    private string logPrefix = "[ProTV Subtitles] ";

    private Vector2[] subtitlesTimes;
    private string[] subtitlesText;
    private int currentSubtitleIndex = 0;
    private bool displayingCurrentIndex = false;
    private bool subtitlesLoaded = false;


    
    

    public void LoadSubtitlesFromString(string subtitles)
    {
        int len = subtitles.Split(new string[] {"-->"}, StringSplitOptions.None).Length - 1;
        subtitlesTimes = new Vector2[len];
        subtitlesText = new string[len];
        
        string[] lines = subtitles
            .Replace("\r\n", "\n")
            .Split('\n');

        int subtitleIndex = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("-->"))
            {
                string[] timeStrings = lines[i].Split(new string[] {"-->"}, StringSplitOptions.None);

                subtitlesTimes[subtitleIndex] = new Vector2(parseTime(timeStrings[0]), parseTime(timeStrings[1]));
                string t = "";
                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (j < lines.Length - 2 && lines[j + 1].Contains("-->"))
                    {
                        break;
                    }
                    else if (lines[j] != "")
                    {
                        t += lines[j] + "\n";
                    }
                }

                if (t.Contains("<font") && t.Contains("</font>") && t.Contains("color=\""))
                {
                    int colorStart = t.IndexOf("color=\"");
                    int colorEnd = t.IndexOf("\"", colorStart + 7);
                    
                    string color = t.Substring(colorStart + 7, colorEnd - colorStart - 7);
                    
                    if (!color.StartsWith("#"))
                    {
                        color = ColorNameToHex(color);
                    }

                    int fontTagStart = t.IndexOf("<font");
                    int fontTagEnd = t.IndexOf(">", fontTagStart + 1);
                    
                    t = t.Remove(fontTagStart, fontTagEnd - fontTagStart + 1);
                    t = t.Insert(fontTagStart, $"<{color}>");
                    
                    t = t.Replace("</font>", "</color>");

                } else if (t.Contains("<font"))
                {
                    int fontTagStart = t.IndexOf("<font");
                    int fontTagEnd = t.IndexOf(">", fontTagStart + 1);
                    
                    t.Remove(fontTagStart, fontTagEnd - fontTagStart + 1);
                }

                subtitlesText[subtitleIndex] = t.Remove(t.Length - 1);
                
                subtitleIndex++;
            }
        }

        currentSubtitleIndex = 0;
        displayingCurrentIndex = false;
        subtitlesLoaded = true;
        
        Debug.Log($"{logPrefix}Loaded {len} subtitles");
    }


    private float parseTime(string inp)
    {
        inp = inp.Replace(",", ".");
        string[] units = inp.Split(':');

        if (units.Length == 2)
        {
            return (float.Parse(units[0]) * 60f) + float.Parse(units[1]);
        }
        
        if (units.Length == 3)
        {
            return (float.Parse(units[0]) * 3600f) + (float.Parse(units[1]) * 60f) + float.Parse(units[2]);
        }

        Debug.LogError($"{logPrefix}Unsupported amount of time units! Expected 2 or 3, got {units.Length}");
        return 0f;
    }
    
    
    public void LoadSubtitlesFromURL(VRCUrl url)
    {
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
        Debug.Log($"{logPrefix}Downloading subtitles from " + url.ToString());
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        Debug.Log($"{logPrefix}Subtitles downloaded successfully");
        LoadSubtitlesFromString(result.Result);
    }
    
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.LogError($"{logPrefix}Subtitles download failed: [{result.ErrorCode}] {result.Error}");
    }


    private void Update()
    {
        if (tvManager == null || !subtitlesEnabled || !subtitlesLoaded)
            return;

        float time = tvManager.currentTime;


        if (!displayingCurrentIndex && time >= subtitlesTimes[currentSubtitleIndex].x)
        {
            subtitleTextObj.text = subtitlesText[currentSubtitleIndex];
            displayingCurrentIndex = true;
        } else if (time >= subtitlesTimes[currentSubtitleIndex].y && currentSubtitleIndex < subtitlesTimes.Length - 1)
        {
            currentSubtitleIndex++;

            if (time >= subtitlesTimes[currentSubtitleIndex].x)
            {
                subtitleTextObj.text = subtitlesText[currentSubtitleIndex];
                displayingCurrentIndex = true;
            }
            else
            {
                subtitleTextObj.text = "";
                displayingCurrentIndex = false;
            }
        }

        if 
        (
            !(
                currentSubtitleIndex == 0 ||
                (
                    time >= subtitlesTimes[currentSubtitleIndex - 1].y  &&
                    time <= subtitlesTimes[currentSubtitleIndex].y
                )
            )
        )
        {
            resyncSubtitleIndex();
            return;
        }
    }

    private void resyncSubtitleIndex()
    {
        Debug.Log($"{logPrefix}Re-syncing subtitle index...");
        float time = tvManager.currentTime;
        for (int i = 0; i < subtitlesTimes.Length; i++)
        {
            if (time <= subtitlesTimes[i].y)
            {
                currentSubtitleIndex = i;
                subtitleTextObj.text = subtitlesText[currentSubtitleIndex];
                return;
            }
        }
    }
    private string[] colorNames = new string[] {"IndianRed","LightCoral","Salmon","DarkSalmon","LightSalmon","Crimson","Red","FireBrick","DarkRed","Pink","LightPink","HotPink","DeepPink","MediumVioletRed","PaleVioletRed","LightSalmon","Coral","Tomato","OrangeRed","DarkOrange","Orange","Gold","Yellow","LightYellow","LemonChiffon","LightGoldenrodYellow","PapayaWhip","Moccasin","PeachPuff","PaleGoldenrod","Khaki","DarkKhaki","Lavender","Thistle","Plum","Violet","Orchid","Fuchsia","Magenta","MediumOrchid","MediumPurple","Amethyst","BlueViolet","DarkViolet","DarkOrchid","DarkMagenta","Purple","Indigo","SlateBlue","DarkSlateBlue","MediumSlateBlue","GreenYellow","Chartreuse","LawnGreen","Lime","LimeGreen","PaleGreen","LightGreen","MediumSpringGreen","SpringGreen","MediumSeaGreen","SeaGreen","ForestGreen","Green","DarkGreen","YellowGreen","OliveDrab","Olive","DarkOliveGreen","MediumAquamarine","DarkSeaGreen","LightSeaGreen","DarkCyan","Teal","Aqua","Cyan","LightCyan","PaleTurquoise","Aquamarine","Turquoise","MediumTurquoise","DarkTurquoise","CadetBlue","SteelBlue","LightSteelBlue","PowderBlue","LightBlue","SkyBlue","LightSkyBlue","DeepSkyBlue","DodgerBlue","CornflowerBlue","MediumSlateBlue","RoyalBlue","Blue","MediumBlue","DarkBlue","Navy","MidnightBlue","Cornsilk","BlanchedAlmond","Bisque","NavajoWhite","Wheat","BurlyWood","Tan","RosyBrown","SandyBrown","Goldenrod","DarkGoldenrod","Peru","Chocolate","SaddleBrown","Sienna","Brown","Maroon","White","Snow","Honeydew","MintCream","Azure","AliceBlue","GhostWhite","WhiteSmoke","Seashell","Beige","OldLace","FloralWhite","Ivory","AntiqueWhite","Linen","LavenderBlush","MistyRose","Gainsboro","LightGrey","Silver","DarkGray","Gray","DimGray","LightSlateGray","SlateGray","DarkSlateGray","Black"};
    private string[] colorHex = new string[] {"CD5C5C","F08080","FA8072","E9967A","FFA07A","DC143C","FF0000","B22222","8B0000","FFC0CB","FFB6C1","FF69B4","FF1493","C71585","DB7093","FFA07A","FF7F50","FF6347","FF4500","FF8C00","FFA500","FFD700","FFFF00","FFFFE0","FFFACD","FAFAD2","FFEFD5","FFE4B5","FFDAB9","EEE8AA","F0E68C","BDB76B","E6E6FA","D8BFD8","DDA0DD","EE82EE","DA70D6","FF00FF","FF00FF","BA55D3","9370DB","9966CC","8A2BE2","9400D3","9932CC","8B008B","800080","4B0082","6A5ACD","483D8B","7B68EE","ADFF2F","7FFF00","7CFC00","00FF00","32CD32","98FB98","90EE90","00FA9A","00FF7F","3CB371","2E8B57","228B22","008000","006400","9ACD32","6B8E23","808000","556B2F","66CDAA","8FBC8F","20B2AA","008B8B","008080","00FFFF","00FFFF","E0FFFF","AFEEEE","7FFFD4","40E0D0","48D1CC","00CED1","5F9EA0","4682B4","B0C4DE","B0E0E6","ADD8E6","87CEEB","87CEFA","00BFFF","1E90FF","6495ED","7B68EE","4169E1","0000FF","0000CD","00008B","000080","191970","FFF8DC","FFEBCD","FFE4C4","FFDEAD","F5DEB3","DEB887","D2B48C","BC8F8F","F4A460","DAA520","B8860B","CD853F","D2691E","8B4513","A0522D","A52A2A","800000","FFFFFF","FFFAFA","F0FFF0","F5FFFA","F0FFFF","F0F8FF","F8F8FF","F5F5F5","FFF5EE","F5F5DC","FDF5E6","FFFAF0","FFFFF0","FAEBD7","FAF0E6","FFF0F5","FFE4E1","DCDCDC","D3D3D3","C0C0C0","A9A9A9","808080","696969","778899","708090","2F4F4F","000000"};
        
    
    private string ColorNameToHex(string name)
    {
        for (int i = 0; i < colorNames.Length; i++)
        {
            if (colorNames[i] == name)
            {
                return $"#{colorHex[i]}";
            }
        }

        return "#FFFFFF";
    }
}
