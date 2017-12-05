using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SkypeFinalBot.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Web.Configuration;
    using Newtonsoft.Json;

    public class MicrosoftCognitiveSpeechService
    {
        private readonly string subscriptionKey;
        private readonly string speechRecognitionUri;

        public MicrosoftCognitiveSpeechService()
        {
            this.DefaultLocale = "en-US";
            this.subscriptionKey = WebConfigurationManager.AppSettings["MicrosoftSpeechApiKey"];
            this.speechRecognitionUri = Uri.UnescapeDataString(WebConfigurationManager.AppSettings["MicrosoftSpeechRecognitionUri"]);
        }

        public string DefaultLocale { get; set; }

        /// <summary>
        /// Gets text from an audio stream.
        /// </summary>
        /// <param name="audiostream"></param>
        /// <returns>Transcribed text. </returns>
        public async Task<string> GetTextFromAudioAsync(Stream audiostream)
        {
            var requestUri = speechRecognitionUri + Guid.NewGuid();

            using (var client = new HttpClient())
            {
                var token = Authentication.Instance.GetAccessToken();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                try
                {
                    using (var binaryContent = new ByteArrayContent(StreamToBytes(audiostream)))
                    {
                        binaryContent.Headers.TryAddWithoutValidation("content-type", "audio/wav; codec=\"audio/pcm\"; samplerate=16000");
                        var response = await client.PostAsync(requestUri, binaryContent);
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            throw new HttpException((int)response.StatusCode, $"({response.StatusCode}) {response.ReasonPhrase}");
                        }
                        var responseString = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrEmpty(responseString))
                        {
                            return "Returned string was empty";
                        }

                        dynamic data = JsonConvert.DeserializeObject(responseString);

                        if (data != null)
                        {
                            if (data["DisplayText"] != null)
                                return data["DisplayText"];
                            else
                            {
                                return "REPEAT";
                            }
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                }
                catch (Exception exp)
                {
                    Debug.WriteLine(exp);
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Converts Stream into byte[].
        /// </summary>
        /// <param name="input">Input stream</param>
        /// <returns>Output byte[]</returns>
        private static byte[] StreamToBytes(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}