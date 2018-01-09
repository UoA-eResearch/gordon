using IBM.Watson.DeveloperCloud.Connection;
using IBM.Watson.DeveloperCloud.Services.ToneAnalyzer.v3;
using IBM.Watson.DeveloperCloud.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;

public class MicrophoneManager : MonoBehaviour
{

	public Text debug;
	private DictationRecognizer dictationRecognizer;
	private ToneAnalyzer toneAnalyzer;
	private Dictionary<string, Color> emotionColors;
	private Material mat;
	private List<Color> currentColors;
	private List<Color> defaultColors;
	private float interval = 2;

	// Use this for initialization
	void Start()
	{
		var credentials = new Credentials("f0fa6e61-82e7-4bf6-adeb-7392e0253c8b", "T8lCuYlwEeVI", "https://gateway.watsonplatform.net/tone-analyzer/api");

		//{ "document_tone":{ "tone_categories":[{"tones":[{"score":0.015361,"tone_id":"anger","tone_name":"Anger"},{"score":0.007565,"tone_id":"disgust","tone_name":"Disgust"},{"score":0.015474,"tone_id":"fear","tone_name":"Fear"},{"score":0.898416,"tone_id":"joy","tone_name":"Joy"},{"score":0.023349,"tone_id":"sadness","tone_name":"Sadness"}],"category_id":"emotion_tone","category_name":"Emotion Tone"},
		//{ "tones":[{"score":0.0,"tone_id":"analytical","tone_name":"Analytical"},{"score":0.0,"tone_id":"confident","tone_name":"Confident"},{"score":0.0,"tone_id":"tentative","tone_name":"Tentative"}],"category_id":"language_tone","category_name":"Language Tone"},
		//{ "tones":[{"score":0.064835,"tone_id":"openness_big5","tone_name":"Openness"},{"score":0.276417,"tone_id":"conscientiousness_big5","tone_name":"Conscientiousness"},{"score":0.613644,"tone_id":"extraversion_big5","tone_name":"Extraversion"},{"score":0.363286,"tone_id":"agreeableness_big5","tone_name":"Agreeableness"},{"score":0.785214,"tone_id":"emotional_range_big5","tone_name":"Emotional Range"}],"category_id":"social_tone","category_name":"Social Tone"}]}}
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

		toneAnalyzer = new ToneAnalyzer(credentials);
		toneAnalyzer.VersionDate = "2017-05-26";
		//toneAnalyzer.GetToneAnalyze(OnGetToneAnalyze, OnFail, "I hate these new features On #ThisPhone after the update.");

		dictationRecognizer = new DictationRecognizer();

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
			if (text.Contains("debug off"))
			{
				debug.gameObject.SetActive(false);
			}
			else if (text.Contains("debug on"))
			{
				debug.gameObject.SetActive(true);
			} else if (text.Contains("come here"))
			{
				gameObject.transform.position = Camera.main.transform.position + Camera.main.transform.forward;
			}
			else
			{
				if (!toneAnalyzer.GetToneAnalyze(OnGetToneAnalyze, OnFail, text))
				{
					outText = "failed to analyze tone";
					Debug.Log(outText);
					debug.text = outText;
				}
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
			Debug.Log(outText);
			debug.text = outText;
			dictationRecognizer.Start();
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

	private void OnGetToneAnalyze(ToneAnalyzerResponse resp, Dictionary<string, object> customData)
	{
		var outText = "tone analyzer response: \n";
		var toneCats = resp.document_tone.tone_categories.ToDictionary(x => x.category_id, x => x.tones);
		var significantEmotions = toneCats["emotion_tone"].Where(x => x.score > .5f);
		currentColors = significantEmotions.Select(x => emotionColors[x.tone_id]).ToList();
		if (significantEmotions.Count() == 0)
		{
			currentColors = defaultColors;
		}
		else if (significantEmotions.Count() == 1)
		{
			currentColors.Add(Color.white);
		}
		foreach (var emotion in significantEmotions)
		{
			outText += emotion.tone_id + ": " + emotion.score + "=" + emotionColors[emotion.tone_id] + "\n";
		}
		Debug.Log(outText);
		debug.text = outText;
	}

	private void OnFail(RESTConnector.Error error, Dictionary<string, object> customData)
	{
		var outText = "tone analyzer error: " + error.ToString();
		Debug.Log(outText);
		debug.text = outText;
	}
}
