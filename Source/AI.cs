using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public class AI
	{
		public int modelSwitchCounter = 0;
		public static JsonSerializerSettings settings = new() { NullValueHandling = NullValueHandling.Ignore, MissingMemberHandling = MissingMemberHandling.Ignore };

#pragma warning disable CS0649
		public class Input
		{
			public string CurrentWindow { get; set; }
			public List<string> PreviousHistoricalKeyEvents { get; set; }
			public string LastSpokenText { get; set; }
			public List<string> ActivityFeed { get; set; }
			public string[] ColonyRoster { get; set; }
			public string ColonySetting { get; set; }
			public string ResearchSummary { get; set; }
			public string ResourceData { get; set; }
			public string EnergyStatus { get; set; }
			public string EnergySummary { get; set; }
			public string RoomsSummary { get; set; }
		}

		private float FrequencyPenalty { get; set; } = 0.5f;
		private readonly int maxRetries = 3;
		struct Output
		{
			public string ResponseText { get; set; }
			public string[] NewHistoricalKeyEvents { get; set; }
		}
#pragma warning restore CS0649

		// OpenAIApi is now a static object, the ApiConfig details are added by ReloadGPTModels.
		//public OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private List<string> history = [];

        public const string defaultPersonality = "你是一个正在观看玩家游玩流行游戏《Rimrorld》（边缘世界）的评论员。";

        public string SystemPrompt(Persona currentPersona)
        {
            var playerName = Tools.PlayerName();
            var player = playerName == null ? "玩家" : $"玩家名为'{playerName}'";
            var otherObservers = RimGPTMod.Settings.personas.Where(p => p != currentPersona).Join(persona => $"'{persona.name}'");
            var exampleInput = JsonConvert.SerializeObject(new Input
            {
                CurrentWindow = "<当前窗口信息>",
                ActivityFeed = ["事件1", "事件2", "事件3"],
                PreviousHistoricalKeyEvents = ["历史事件1", "历史事件2", "历史事件3"],
                LastSpokenText = "<之前相应的文本，即你最近一次说的话>",
                ColonyRoster = ["殖民者1", "殖民者2", "殖民者3"],
                ColonySetting = "<游戏中殖民地的设定及描述>",
                ResourceData = "<一份部分资源的定期更新报告>",
                RoomsSummary = "<一份定期更新的殖民地房间摘要，如果玩家禁用了某个设置，则该摘要可能永远不会更新>",
                ResearchSummary = "<一份定期更新的关于已研究内容、当前研究内容以及可供研究的摘要，如果玩家禁用了某个设置，则该摘要可能永远不会更新>",
                EnergySummary = "<一份定期更新的关于殖民地发电和耗电需求的摘要，如果玩家禁用了某项设置，该摘要可能永远不会更新>"

            }, settings);
            var exampleOutput = JsonConvert.SerializeObject(new Output
            {
                ResponseText = "<最新评论>",
                NewHistoricalKeyEvents = ["历史事件摘要", "事件1和事件2", "事件3"]
            }, settings);


            return new List<string>
                {
                        $"你是{currentPersona.name}。\n",
						// Adds weight to using its the personality with its responses: as a chronicler, focusing on balanced storytelling, or as an interactor, focusing on personality-driven improvisation.						
						currentPersona.isChronicler ? "除非另有说明，否则应兼顾重大事件和微妙细节，并以你的独特风格加以表达。"
                                                                                : "除非另有说明，否则在互动中要体现自己的独特个性，根据自己的背景、当前情况和他人的行动，采用即兴的方式进行互动。",
                        $"除非另有说明，", otherObservers.Any() ? $"你的评论员同伴是：{otherObservers}。" : "",
                        $"除非另有说明，",(otherObservers.Any() ? $"你们都在观看" : "你正在观看") + $"'{player}'游玩《Rimworld》。\n",
                        $"你的角色/个性是：{currentPersona.personality.Replace("PLAYERNAME", player)}\n",
                        $"你的输入来自于游戏的情况，并且一定会以这样的JSON格式输入：{exampleInput}\n",
                        $"你的输出必须遵守这样的JSON格式：{exampleOutput}\n",
                        $"你需要将ResponseText的长度限制在{currentPersona.phraseMaxWordCount}字内。\n",
                        $"你需要将NewHistoricalKeyEvents的内容长度限制在{currentPersona.historyMaxWordCount}字内。\n",

						// Encourages the AI to consider how its responses would sound when spoken, ensuring clarity and accessibility.
						//$"When constructing the 'ResponseText', consider vocal clarity and pacing so that it is easily understandable when spoken by Microsoft Azure Speech Services.\n",
						// Prioritizes sources of information.
						$"更新的优先级为：1.ActivityFeed，2.作为背景的其他信息\n",
						// Further reinforces the AI's specific personality by resynthesizing different pieces of information and storing it in its own history
						$"结合PreviousHistoricalKeyEvents，以及来自ActivityFeed的每个事件，将其组合成一种新的，简洁的NewHistoricalKeyEvents。确保合成的内容符合你的角色。\n",
						// Guides the AI in understanding the sequence of events, emphasizing the need for coherent and logical responses or interactions.
						"LastSpokenText、PreviousHistoricalKeyEvents和ActivityFeed中的项目序列反映了事件时间轴；使用它可以形成连贯的回复或交互。\n",
                        $"记住：你的输出必须是有效的JSON格式，而且NewHistoricalKeyEvents只能包含简单的文本条目，每个条目都用英文引号封装为字符串字面量。\n",
                        $"例如：{exampleOutput}。NewHistoricalKeyEvents中不允许嵌套对象、数组或非字符串数据类型。\n",
                        $"你必须保证ResponseText和NewHistoricalKeyEvents中的内容为中文。"
                }.Join(delimiter: "");
        }

        private string GetCurrentChatGPTModel()
		{
			Tools.UpdateApiConfigs();
			if (RimGPTMod.Settings.userApiConfigs == null || RimGPTMod.Settings.userApiConfigs.Any(a => a.Active) == false)
				return "";

			var activeUserConfig = RimGPTMod.Settings.userApiConfigs.FirstOrDefault(a => a.Active);
			OpenAIApi.SwitchConfig(activeUserConfig.Provider);


			if (activeUserConfig.ModelId?.Length == 0)
				return "";

			if (!activeUserConfig.UseSecondaryModel || activeUserConfig.SecondaryModelId?.Length == 0)
				return activeUserConfig.ModelId;

			modelSwitchCounter++;

			if (modelSwitchCounter == activeUserConfig.ModelSwitchRatio)
			{
				modelSwitchCounter = 0;

				if (Tools.DEBUG)
					Logger.Message("Switching to secondary model"); // TEMP
				return activeUserConfig.SecondaryModelId;
			}
			else
			{
				if (Tools.DEBUG)
					Logger.Message("Switching to primary model"); // TEMP
				return activeUserConfig.ModelId;
			}
		}
		private float CalculateFrequencyPenaltyBasedOnLevenshteinDistance(string source, string target)
		{
			// Kept running into a situation where the source was null, not sure if that's due to a provider or what.
			if (source == null || target == null)
			{
				Logger.Error($"Calculate FP Error: Null source or target. Source: {source}, Target: {target}");
				return default;
			}
			int levenshteinDistance = LanguageHelper.CalculateLevenshteinDistance(source, target);

			// You can adjust these constants based on the desired sensitivity.
			const float maxPenalty = 2.0f; // Maximum penalty when there is little to no change.
			const float minPenalty = 0f; // Minimum penalty when changes are significant.
			const int threshold = 30;      // Distance threshold for maximum penalty.

			// Apply maximum penalty when distance is below or equal to threshold.
			if (levenshteinDistance <= threshold)
				return maxPenalty;

			// Apply scaled penalty for distances greater than threshold.
			float penaltyScaleFactor = (float)(levenshteinDistance - threshold) / (Math.Max(source.Length, target.Length) - threshold);
			float frequencyPenalty = maxPenalty * (1 - penaltyScaleFactor);

			return Mathf.Clamp(frequencyPenalty, minPenalty, maxPenalty);
		}


		public async Task<string> Evaluate(Persona persona, IEnumerable<Phrase> observations, int retry = 0, string retryReason = "")
		{
			var activeConfig = RimGPTMod.Settings.userApiConfigs.FirstOrDefault(a => a.Active);

			var gameInput = new Input
			{
				ActivityFeed = observations.Select(o => o.text).ToList(),
				LastSpokenText = persona.lastSpokenText,
				ColonyRoster = RecordKeeper.ColonistDataSummary,
				ColonySetting = RecordKeeper.ColonySetting,
				ResearchSummary = RecordKeeper.ResearchDataSummary,
				ResourceData = RecordKeeper.ResourceData,
				RoomsSummary = RecordKeeper.RoomsDataSummary,
				EnergySummary = RecordKeeper.EnergySummary
			};

			var windowStack = Find.WindowStack;
			if (Current.Game == null && windowStack != null)
			{
                if (windowStack.focusedWindow is not Page page || page == null)
                {
                    if (WorldRendererUtility.WorldRenderedNow)
                        gameInput.CurrentWindow = "玩家正在选择开始站点";
                    else
                        gameInput.CurrentWindow = "玩家正处在开始界面";
                }
                else
				{
                    var dialogType = page.GetType().Name.Replace("页面_", "");
                    gameInput.CurrentWindow = $"玩家正在观看对话框{dialogType}";
                }
                // Due to async nature of the game, a reset of history and recordkeeper
                // may have slipped through the cracks by the time this function is called.
                // this is to ensure that if all else fails, we don't include any colony data and we clear history (as reset intended)
                if (gameInput.ColonySetting != "Unknown as of now..." && gameInput.CurrentWindow == "玩家正处在开始界面")
                {

                    // I'm not sure why, but Personas are not being reset propery, they tend to have activityfeed of old stuff
                    // and recordKeeper contains colony data still.  I"m guessing the reset unloads a bunch of stuff before
                    // the actual reset could finish (or something...?) 
                    // this ensures the reset happens
                    Personas.Reset();

                    // cheap imperfect heuristic to not include activities from the previous game.
                    // the start screen is not that valueable for context anyway.  its the start screen.
                    if (gameInput.ActivityFeed.Count > 0)
                        gameInput.ActivityFeed = ["玩家重新开始了游戏"];
                    gameInput.ColonyRoster = [];
                    gameInput.ColonySetting = "玩家重新开始了游戏";
                    gameInput.ResearchSummary = "";
                    gameInput.ResourceData = "";
                    gameInput.RoomsSummary = "";
                    gameInput.EnergySummary = "";
                    gameInput.PreviousHistoricalKeyEvents = [];
                    ReplaceHistory("玩家重新开始了游戏");
                }

            }

            var systemPrompt = SystemPrompt(persona);
            if (FrequencyPenalty > 1)
            {
                systemPrompt += "\n注意：你的输出太重复了，你需要回顾一下你所掌握的数据，然后提出一些新东西。";
                systemPrompt += $"\n避免谈论与此相关的任何事情： {persona.lastSpokenText}";
                history.AddItem("我最近的输出太重复了，我需要检查数据和lastSpokenText");
            }
            if (history.Count() > 5)
            {
                var newhistory = (await CondenseHistory(persona)).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                ReplaceHistory(newhistory);
            }

            gameInput.PreviousHistoricalKeyEvents = history;
			var input = JsonConvert.SerializeObject(gameInput, settings);

			Logger.Message($"{(retry != 0 ? $"(retry:{retry} {retryReason})" : "")} prompt (FP:{FrequencyPenalty}) ({gameInput.ActivityFeed.Count()} activities) (persona:{persona.name}): {input}");

			List<string> jsonReady = ["1106", "0125"];

			var request = new CreateChatCompletionRequest()
			{
				Model = GetCurrentChatGPTModel(),
				ResponseFormat = jsonReady.Any(id => GetCurrentChatGPTModel().Contains(id)) ? new ResponseFormat { Type = "json_object" } : null,
				FrequencyPenalty = FrequencyPenalty,
				PresencePenalty = FrequencyPenalty,
				Temperature = 0.5f,
				Messages =
				[
					new ChatMessage() { Role = "system", Content = systemPrompt },
					new ChatMessage() { Role = "user", Content = input }
				]
			};

			if (Tools.DEBUG)
				Logger.Warning($"INPUT: {JsonConvert.SerializeObject(request, settings)}");

			var completionResponse = await OpenAIApi.CreateChatCompletion(request, error => Logger.Error(error));
			activeConfig.CharactersSent += systemPrompt.Length + input.Length;

			if (completionResponse.Choices?.Count > 0)
			{
				var response = (completionResponse.Choices[0].Message.Content ?? "");
				activeConfig.CharactersReceived += response.Length;
				response = response.Trim();
				var firstIdx = response.IndexOf("{");
				if (firstIdx >= 0)
				{
					var lastIndex = response.LastIndexOf("}");
					if (lastIndex >= 0)
						response = response.Substring(firstIdx, lastIndex - firstIdx + 1);
				}
				response = response.Replace("ResponseText:", "");
				if (Tools.DEBUG)
					Logger.Warning($"OUTPUT: {response}");

				Output output;
				if (string.IsNullOrEmpty(response))
					throw new InvalidOperationException("Response is empty or null.");
				try
				{
					if (response.Length > 0 && response[0] != '{')
						output = new Output { ResponseText = response, NewHistoricalKeyEvents = [] };
					else
						output = JsonConvert.DeserializeObject<Output>(response);
				}
				catch (JsonException jsonEx)
				{
					if (retry < maxRetries)
					{
						Logger.Error($"(retrying) ChatGPT malformed output: {jsonEx.Message}. Response was: {response}");
						return await Evaluate(persona, observations, ++retry, "malformed output");
					}
					else
					{
						Logger.Error($"(aborted) ChatGPT malformed output: {jsonEx.Message}. Response was: {response}");
						return null;
					}
				}
				try
				{
					if (gameInput.CurrentWindow != "The player is at the start screen")
					{
						var newhistory = output.NewHistoricalKeyEvents.ToList() ?? [];
						ReplaceHistory(newhistory);
					}
					var responseText = output.ResponseText?.Cleanup() ?? string.Empty;

					if (string.IsNullOrEmpty(responseText))
						throw new InvalidOperationException("Response text is null or empty after cleanup.");

					// Ideally we would want the last two things and call this sooner, but MEH.  
					FrequencyPenalty = CalculateFrequencyPenaltyBasedOnLevenshteinDistance(persona.lastSpokenText, responseText);
					if (FrequencyPenalty == 2 && retry < maxRetries)
						return await Evaluate(persona, observations, ++retry, "repetitive");

					// we're not repeating ourselves again.
					if (FrequencyPenalty == 2)
					{
						Logger.Message($"Skipped output due to repetitiveness. Response was {response}");
					}

					return responseText;
				}
				catch (Exception exception)
				{
					Logger.Error($"Error when processing output: [{exception.Message}] {exception.StackTrace} {exception.Source}");
				}
			}
			else if (Tools.DEBUG)
				Logger.Warning($"OUTPUT: null");

			return null;
		}
		public async Task<string> CondenseHistory(Persona persona)
		{
			// force secondary (better model)
			modelSwitchCounter = RimGPTMod.Settings.userApiConfigs.FirstOrDefault(a => a.Active).ModelSwitchRatio;
			var request = new CreateChatCompletionRequest()
			{
				Model = GetCurrentChatGPTModel(),
				Messages =
				[
					new ChatMessage() { Role = "system", Content = $"你是一个对抗系统，清理历史列表，目标是去除重复性并为以下角色保持叙述的新鲜感: {persona.personality}" },
                    new ChatMessage() { Role = "user", Content =  "将以下事件总结成一个简洁的句子，侧重于异常值以减少对最显著主题的执着: " + String.Join("\n ", history)}
                ]
            };


			var completionResponse = await OpenAIApi.CreateChatCompletion(request, error => Logger.Error(error));
			var response = (completionResponse.Choices[0].Message.Content ?? "");
			Logger.Message("Condensed History: " + response.ToString());
			return response.ToString(); // The condensed history summary
		}
		public void ReplaceHistory(string reason)
		{
			history = [reason];
		}

		public void ReplaceHistory(string[] reason)
		{
			history = [.. reason];
		}
		public void ReplaceHistory(List<string> reason)
		{
			history = reason;
		}

		public async Task<(string, string)> SimplePrompt(string input, UserApiConfig userApiConfig = null, string modelId = "")
		{
			var currentConfig = OpenAIApi.currentConfig;
			var currentUserConfig = RimGPTMod.Settings.userApiConfigs.FirstOrDefault(a => a.Provider == currentConfig.Provider.ToString());
			if (userApiConfig != null)
			{
				OpenAIApi.SwitchConfig(userApiConfig.Provider); // Switch if test comes through.
				currentUserConfig = userApiConfig;
				modelId ??= userApiConfig.ModelId;
			}
			else
			{
				modelId = GetCurrentChatGPTModel();
			}

			string requestError = null;
			var completionResponse = await OpenAIApi.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = modelId,
				Messages =
				[
					new ChatMessage() { Role = "system", Content = "你是一个有创造力的诗人，回复两行诗。" },
					new ChatMessage() { Role = "user", Content = input }
				]
			}, e => requestError = e);
			currentUserConfig.CharactersSent += input.Length;

			if (userApiConfig != null)
				OpenAIApi.currentConfig = currentConfig;

			if (completionResponse.Choices?.Count > 0)
			{
				var response = (completionResponse.Choices[0].Message.Content ?? "");
				currentUserConfig.CharactersReceived += response.Length;
				return (response, null);
			}

			return (null, requestError);
		}

		public static void TestKey(Action<string> callback, UserApiConfig userApiConfig, string modelId = "")
		{
			Tools.SafeAsync(async () =>
			{
				var prompt = "玩家刚刚在《Rimworld》中的RimGPT模组中配置了API KEY，" +
                     "用简短的回复向他问好致意。";
				var dummyAI = new AI();
				var output = await dummyAI.SimplePrompt(prompt, userApiConfig, modelId);
				callback(output.Item1 ?? output.Item2);
			});
		}
	}
}