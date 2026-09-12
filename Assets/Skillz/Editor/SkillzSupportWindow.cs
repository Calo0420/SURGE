#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SkillzSDK;
using SkillzSDK.Settings;

public sealed class SkillzSupportWindow : EditorWindow
{

  private GUIStyle linkStyle;
  private GUIStyle titleStyle;
  private GUIStyle textStyle;
  private GUIStyle horizontalLine;

  private Color titleColor = new Color(.8f, .8f, .8f);
  private Color lineColor = new Color(.5f, .5f, .5f);
  private Color linkColor = new Color(.2f, .55f, .93f);

  private string clickedLinkURL = null;
  private bool isLinkClicked = false;

  [MenuItem("Skillz/Support", priority = -1)]

  private static void Open()
  {
    var window = GetWindow<SkillzSupportWindow>();
    window.titleContent.text = "Support";
    window.minSize = new Vector2(300, 400);

    window.Show();
  }

  private void OnGUI()
  {
    InitializeStyles();
    Links();
  }

  private void Links()
  {

    EditorGUILayout.Space();
    EditorGUILayout.Space();

    EditorGUILayout.LabelField("Developer Resources", titleStyle);

    DrawHorizontalLine();

    EditorGUILayout.Space();

    RenderLinkButton("Skillz Developer Documentation", "https://docs.skillz.com/");
    RenderLinkButton("Skillz Developer Console", "https://developers.skillz.com/");

    EditorGUILayout.Space();

    EditorGUILayout.LabelField("Support", titleStyle);

    DrawHorizontalLine();

    EditorGUILayout.Space();

    RenderLinkButton("Report A Bug", "https://developers.skillz.com/support/contact_us");
    RenderLinkButton("Contact Support", "https://developers.skillz.com/support/contact_us");

    EditorGUILayout.Space();

    EditorGUILayout.LabelField("About", titleStyle);

    DrawHorizontalLine();

    EditorGUI.indentLevel++;
    EditorGUILayout.Space();

    EditorGUILayout.LabelField("Version: 2025.0.50", textStyle);
  }

  private void RenderLinkButton(string label, string url)
  {
    EditorGUILayout.BeginHorizontal();

    EditorGUILayout.Space(10f, false);

    if (GUILayout.Button(label, linkStyle))
    {
      isLinkClicked = true;
      clickedLinkURL = url;
    }

    EditorGUILayout.EndHorizontal();
  }

  private void Update()
  {
    if (isLinkClicked)
    {
      isLinkClicked = false;
      Application.OpenURL(clickedLinkURL);
    }
  }

  private void InitializeStyles()
  {
    //style for links
    linkStyle = new GUIStyle()
    {
      richText = true,
      fontSize = 16,
      fontStyle = FontStyle.Bold,

    };
    linkStyle.normal.textColor = linkColor;

    //style for title labels
    titleStyle = new GUIStyle()
    {
      fontSize = 18,
      fontStyle = FontStyle.Bold
    };
    titleStyle.normal.textColor = titleColor;

    //style for labels
    textStyle = new GUIStyle()
    {
      fontSize = 14,
      fontStyle = FontStyle.Bold
    };
    textStyle.normal.textColor = titleColor;

    //style for horizonal lines
    horizontalLine = new GUIStyle();
    horizontalLine.normal.background = Texture2D.whiteTexture;
    horizontalLine.margin = new RectOffset(0, 0, 4, 4);
    horizontalLine.fixedHeight = 1;
  }

  
  private void DrawHorizontalLine()
  {
    var prevColor = GUI.color;
    GUI.color = lineColor;

    GUILayout.Box(GUIContent.none, horizontalLine);

    GUI.color = prevColor;
  }

}
#endif