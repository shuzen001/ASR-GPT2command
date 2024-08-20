using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Whisper.Utils;

namespace Whisper.Samples
{
    /// <summary>
    /// Stream transcription from microphone input.
    /// </summary>
    public class StreamingSampleMic : MonoBehaviour
    {
        public WhisperManager whisper;
        public MicrophoneRecord microphoneRecord;

        [Header("UI")]
        public Button button;
        public Text buttonText;
        public Text text;
        public ScrollRect scroll;
        private WhisperStream _stream;

        private GptApiClient _client;

        private async void Start()
        {
            _stream = await whisper.CreateStream(microphoneRecord);
            _stream.OnResultUpdated += OnResult;
            _stream.OnSegmentUpdated += OnSegmentUpdated;
            _stream.OnSegmentFinished += OnSegmentFinished;
            _stream.OnStreamFinished += OnFinished;

            microphoneRecord.OnRecordStop += OnRecordStop;
            button.onClick.AddListener(OnButtonPressed);

            string apiKey = ""; // Replace with your OpenAI API key
            _client = new GptApiClient(apiKey);

            string prompt = "";
            string command = await _client.GetCompletionAsync(prompt);

            if (command != null)
            {
                Debug.Log($"Generated Command: {command}");
            }
        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                _stream.StartStream();
                microphoneRecord.StartRecord();
            }
            else
            {
                microphoneRecord.StopRecord();
            }

            buttonText.text = microphoneRecord.IsRecording ? "Stop" : "Record";
        }

        private void OnRecordStop(AudioChunk recordedAudio)
        {
            buttonText.text = "Record";
        }

        private void OnResult(string result)
        {
            //text.text = result;
            //UiUtils.ScrollDown(scroll);
        }

        private void OnSegmentUpdated(WhisperResult segment)
        {
            Debug.Log($"Segment updated: {segment.Result}");
        }

        private async void OnSegmentFinished(WhisperResult segment)
        {
            Debug.Log($"Segment finished: {segment.Result}");

            
            // Convert the transcribed text to a command

            string command = await ConvertToCommandAsync(segment.Result);

            // Process the command (e.g., display on UI or execute it)

            ProcessCommand(command);
            // Display the command in the UI's Text component
            text.text = command;  // 更新 UI 以顯示 GPT-4 回傳的結果
            UiUtils.ScrollDown(scroll);  // 確保 UI 滑動到底部以顯示最新結果
        }

        private async Task<string> ConvertToCommandAsync(string transcript)
        {
            // Call the GPT-3.5 API to convert natural language to a fixed-format command
            string prompt = $"請把我跟你說的每一句話理解，並從[走路、跑步、出去玩、爬坡、跳舞、其他]中選出最接近的command，如果沒有符合的就選其他。Only one command a response without any words else." +
                $"{transcript}";
            string command = await _client.GetCompletionAsync(prompt);

            return command;
        }

        private void ProcessCommand(string command)
        {
            // Handle the command as needed, e.g., display on UI, execute logic, etc.
            Debug.Log("Processed Command: " + command);
            // You can add further logic here to execute these commands

        }

        private void OnFinished(string finalResult)
        {
            Debug.Log("Stream finished!");
        }
    }

    // code below is for requesting completion from openai gpt through api call

    [System.Serializable]
    public class ChatCompletionRequest
    {
        public string model;
        public Message[] messages;
        public int max_tokens;
        public double temperature;
        public bool stream;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ChatCompletionResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
    }

    public class GptApiClient
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GptApiClient(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GetCompletionAsync(string prompt)
        {
            var requestBody = new ChatCompletionRequest
            {
                model = "gpt-4o-mini",
                messages = new Message[]
                {
                    new Message { role = "user", content = prompt }
                },
                max_tokens = 100,
                temperature = 0.7,
                stream = true
            };

            var json = JsonUtility.ToJson(requestBody);

            var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var gptResponse = JsonUtility.FromJson<ChatCompletionResponse>(jsonResponse);
                return gptResponse.choices[0].message.content.Trim();
            }
            else
            {
                Debug.LogError($"OpenAI API error: {response.ReasonPhrase} - {await response.Content.ReadAsStringAsync()}");
                return null;
            }
        }
    }
}