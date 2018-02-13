using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR.WSA.Input;
using UnityEngine.Windows.Speech;

public class MicrophoneManager : MonoBehaviour
{

	public Text debug;
	public AudioSource audioSource;
	public GameObject piece1;
	public GameObject piece2;
	private DictationRecognizer dictationRecognizer;
	private GestureRecognizer manipulationRecognizer;
	private Dictionary<string, Color> emotionColors;
	private List<Color> currentColors;
	private List<Color> defaultColors;
	private Vector3 targetScale;
	private float interval = 2;
	private AudioClip[] clips;
	private bool emotion = true;
	private bool duplicated = false;
	private Dictionary<Transform, Vector3> clones;
	private Vector3 manipulationPreviousPosition = Vector3.zero;
	private Transform selectedObject;
	private float startTime = 0f;

	// Use this for initialization
	void Start()
	{
		emotionColors = new Dictionary<string, Color>()
		{
			{"anger", Color.red},
			{"disgust", Color.green},
			{"fear", Color.magenta},
			{"joy", new Color(1, .5f, 0)},
			{"sadness", Color.blue}
		};
		defaultColors = new List<Color>()
		{
			Color.white
		};
		currentColors = defaultColors;

		clips = Resources.LoadAll<AudioClip>("GordonSounds");

		targetScale = Vector3.one * .01f;

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
			bool command = false;
			if (text.Contains("debug off") || text.Contains("the bug off"))
			{
				debug.gameObject.SetActive(false);
				command = true;
			}
			else if (text.Contains("debug on") || text.Contains("the bug on"))
			{
				debug.gameObject.SetActive(true);
				command = true;
			}
			if (text.Contains("come here"))
			{
				var p = Camera.main.transform.position + Camera.main.transform.forward;
				gameObject.transform.position = new Vector3(p.x, gameObject.transform.position.y, p.z);
				command = true;
			}
			var tempColors = new List<Color>();
			if (text.Contains("red"))
			{
				tempColors.Add(Color.red);
				command = true;
			}
			if (text.Contains("orange"))
			{
				tempColors.Add(new Color(1, .5f, 0));
				command = true;
			}
			if (text.Contains("green"))
			{
				tempColors.Add(Color.green);
				command = true;
			}
			if (text.Contains("blue"))
			{
				tempColors.Add(Color.blue);
				command = true;
			}
			if (text.Contains("purple"))
			{
				tempColors.Add(new Color(.5f, 0, .5f));
				command = true;
			}
			if (text.Contains("pink") || text.Contains("magenta"))
			{
				tempColors.Add(Color.magenta);
				command = true;
			}
			if (text.Contains("white"))
			{
				tempColors.Add(Color.white);
				command = true;
			}
			currentColors = tempColors;
			if (text.Contains("grow") || text.Contains("big") || text.Contains("reset"))
			{
				targetScale = Vector3.one * .01f;
				command = true;
			}
			else if (text.Contains("shrink") || text.Contains("small"))
			{
				targetScale = Vector3.one * .001f;
				command = true;
			}
			if (text.Contains("gordon") || text.Contains("golden") || text.Contains("garden"))
			{
				var i = Random.Range(0, clips.Length);
				audioSource.clip = clips[i];
				audioSource.Play();
				Debug.Log("playing " + clips[i].name);
			}
			if (text.Contains("emotion on"))
			{
				emotion = true;
				command = true;
			}
			else if (text.Contains("emotion off"))
			{
				emotion = false;
				command = true;
			}
			if (text.Contains("duplicate") && !duplicated)
			{
				var prefab = transform.GetChild(0);
				clones = new Dictionary<Transform, Vector3>();
				for (var i = 1; i < 4; i++)
				{
					var clone = Instantiate(prefab, transform);
					var dir = clone.transform.position - Camera.main.transform.position;
					dir = Quaternion.Euler(0, i * 90, 0) * dir;
					var targetPos = Camera.main.transform.position + dir;
					clones.Add(clone, targetPos);
				}
				duplicated = true;
				startTime = Time.time;
				command = true;
			}
			else if ((text.Contains("single") || text.Contains("reset")) && duplicated)
			{
				foreach (var clone in clones)
				{
					Destroy(clone.Key.gameObject);
				}
				duplicated = false;
				command = true;
			}
			if (text.Contains("extend"))
			{
				piece1.SetActive(true);
				piece2.SetActive(true);
				manipulationRecognizer.StartCapturingGestures();
				command = true;
			}
			else if (text.Contains("remove") || text.Contains("reset"))
			{
				piece1.SetActive(false);
				piece2.SetActive(false);
				manipulationRecognizer.StopCapturingGestures();
				command = true;
			}
			if (text.Contains("reset"))
			{
				currentColors = defaultColors;
				command = true;
			}
			if (!command && emotion)
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

		manipulationRecognizer = new GestureRecognizer();
		manipulationRecognizer.SetRecognizableGestures(GestureSettings.ManipulationTranslate);
		manipulationRecognizer.ManipulationUpdatedEvent += ManipulationRecognizer_ManipulationUpdatedEvent;
		manipulationRecognizer.ManipulationCompletedEvent += ManipulationRecognizer_ManipulationCompletedEvent;
		manipulationRecognizer.ManipulationCanceledEvent += ManipulationRecognizer_ManipulationCompletedEvent;

	}

	private void ManipulationRecognizer_ManipulationCompletedEvent(InteractionSourceKind source, Vector3 cumulativeDelta, Ray headRay)
	{
		manipulationPreviousPosition = Vector3.zero;
		selectedObject = null;
	}

	private void ManipulationRecognizer_ManipulationUpdatedEvent(InteractionSourceKind source, Vector3 cumulativeDelta, Ray headRay)
	{
		if (!selectedObject)
		{
			var minDist = float.PositiveInfinity;
			selectedObject = transform;
			foreach (Transform child in transform)
			{
				float distance = Vector3.Cross(headRay.direction, child.position - headRay.origin).magnitude;
				if (distance < minDist)
				{
					minDist = distance;
					selectedObject = child;
				}
			}
			Debug.Log("ray was closest to " + selectedObject.name + " with dist " + minDist);
		}

		var moveVector = cumulativeDelta - manipulationPreviousPosition;
		manipulationPreviousPosition = cumulativeDelta;
		selectedObject.position += moveVector;
	}

	// Update is called once per frame
	void Update()
	{
		if (audioSource.isPlaying)
		{
			if (currentColors.Count == 1)
			{
				currentColors.Add(Color.gray);
			}
			interval = .5f;
		}
		else
		{
			currentColors.Remove(Color.gray);
			interval = 2;
		}

		if (duplicated)
		{
			foreach (var clone in clones)
			{
				clone.Key.position = Vector3.Lerp(clone.Key.position, clone.Value, (Time.time - startTime) / 20);
			}
		}


		if (currentColors.Count > 0)
		{
			var t = Time.time / interval % currentColors.Count;
			int index = (int)t;
			var targetColor = currentColors[index];

			var rends = gameObject.GetComponentsInChildren<Renderer>();
			foreach (var r in rends)
			{
				r.material.color = Color.Lerp(r.material.color, targetColor, Time.time / interval % 1);
			}
		}
		gameObject.transform.localScale = Vector3.Lerp(gameObject.transform.localScale, targetScale, Time.deltaTime);
	}

	IEnumerator GetToneAnalysis(string text)
	{
		var headers = new Dictionary<string, string>() {
			{ "Authorization", "Basic ZjBmYTZlNjEtODJlNy00YmY2LWFkZWItNzM5MmUwMjUzYzhiOlQ4bEN1WWx3RWVWSQ==" },
			{ "Content-Type", "text/plain" }
		};
		var www = new WWW("https://gateway.watsonplatform.net/tone-analyzer/api/v3/tone?version=2018-01-01&sentences=false", Encoding.ASCII.GetBytes(text), headers);
		yield return www;
		string responseString = www.text;
		var outText = www.bytesDownloaded + responseString;
		Debug.Log(outText);
		debug.text = outText;
		var json = new JSONObject(responseString);
		var tones = json["document_tone"]["tones"].list;
		currentColors = tones.Where(x => emotionColors.ContainsKey(x["tone_id"].str)).Select(x => emotionColors[x["tone_id"].str]).ToList();
		if (tones.Count() == 0)
		{
			currentColors = defaultColors;
		}
		else if (tones.Count() == 1)
		{
			//currentColors.Add(Color.white);
		}
	}
}
