using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;

public class MicrophoneManager : MonoBehaviour
{

	public Text debug;
	private DictationRecognizer dictationRecognizer;
	private Dictionary<string, Color> emotionColors;
	private Material mat;
	private List<Color> currentColors;
	private List<Color> defaultColors;
	private float interval = 2;

	// Use this for initialization
	void Start()
	{
		emotionColors = new Dictionary<string, Color>()
		{
			{"anger", Color.red},
			{"disgust", Color.green},
			{"fear", Color.magenta},
			{"joy", Color.yellow},
			{"sadness", Color.blue}
		};
		defaultColors = new List<Color>()
		{
			Color.white,
			Color.gray
		};
		currentColors = defaultColors;

		mat = gameObject.GetComponentInChildren<Renderer>().material;

		//StartCoroutine(GetToneAnalysis("I'm happy"));

		dictationRecognizer = new DictationRecognizer(ConfidenceLevel.Rejected);
		dictationRecognizer.AutoSilenceTimeoutSeconds = 5;

		dictationRecognizer.DictationHypothesis += (text) =>
		{
			var outText = "hypothesis: " + text + "...";
			Debug.Log(outText);
			debug.text = outText;
		};

		dictationRecognizer.DictationResult += (text, confidence) =>
		{
			var outText = "result: " + text;
			Debug.Log(outText);
			debug.text = outText;
			if (text.Contains("debug off") || text.Contains("the bug off"))
			{
				debug.gameObject.SetActive(false);
			}
			else if (text.Contains("debug on") || text.Contains("the bug on"))
			{
				debug.gameObject.SetActive(true);
			} else if (text.Contains("come here"))
			{
				var p = Camera.main.transform.position + Camera.main.transform.forward;
				gameObject.transform.position = new Vector3(p.x, gameObject.transform.position.y, p.z);
			}
			else
			{
				StartCoroutine(GetToneAnalysis(text));
			}
		};

		dictationRecognizer.DictationComplete += (completionCause) =>
		{
			var outText = "dictation complete. restarting...";
			Debug.Log(outText);
			debug.text = outText;
			dictationRecognizer.Start();
		};

		dictationRecognizer.DictationError += (error, hresult) =>
		{
			var outText = "Dictation error: " + error;
			Debug.LogError(outText);
			debug.text = outText;
		};

		dictationRecognizer.Start();
	}

	// Update is called once per frame
	void Update()
	{
		var t = Time.time / interval % currentColors.Count;
		int a = (int)t;
		int b = a + 1;
		if (b >= currentColors.Count)
		{
			b = 0;
		}
		t = Time.time / interval % 1;
		mat.color = Color.Lerp(currentColors[a], currentColors[b], t);
	}

	IEnumerator GetToneAnalysis(string text)
	{
		var www = UnityWebRequest.Post("https://gateway.watsonplatform.net/tone-analyzer/api/v3/tone?version=2018-01-01&sentences=false", text);
		www.SetRequestHeader("Authorization", "Basic ZjBmYTZlNjEtODJlNy00YmY2LWFkZWItNzM5MmUwMjUzYzhiOlQ4bEN1WWx3RWVWSQ==");
		www.SetRequestHeader("Content-Type", "text/plain");
		yield return www.SendWebRequest();

		if (www.isNetworkError || www.isHttpError)
		{
			var outText = www.error;
			debug.text = outText;
			Debug.LogError(outText);
		}
		else
		{
			string responseString = www.downloadHandler.text;
			var json = new JSONObject(responseString);
			var tones = json["document_tone"]["tones"].list;
			var outText = "tone analyzer response: \n";
			currentColors = tones.Select(x => emotionColors[x["tone_id"].str]).ToList();
			if (tones.Count() == 0)
			{
				currentColors = defaultColors;
			}
			else if (tones.Count() == 1)
			{
				currentColors.Add(Color.white);
			}
			foreach (var emotion in tones)
			{
				outText += emotion["tone_id"].str + ": " + emotion["score"].f + "=" + emotionColors[emotion["tone_id"].str] + "\n";
			}
			Debug.Log(outText);
			debug.text = outText;
		}
	}
}
