﻿using UnityEngine;
using Verse;

namespace RimGPT
{
	public partial class RimGPTSettings
	{
		private class DetailedReportSettingsWindow : Window
		{
			// Reference to the settings to allow us to modify them
			private readonly RimGPTSettings settings;

			// Constructor to pass in the settings reference
			public DetailedReportSettingsWindow(RimGPTSettings settings)
			{
				this.settings = settings;

				// Set properties for the window
				doCloseX = true;

			}

			// Override the method to specify the initial size of the window
			public override Vector2 InitialSize => new(800f, 680f);


			public override void DoWindowContents(Rect inRect)
			{
				var listing = new Listing_Standard();
				listing.Begin(inRect);
				Text.Font = GameFont.Medium;
				listing.Label("AI Insights Configuration");

				// Switch back to the small font for standard text and options
				Text.Font = GameFont.Small;

				// Description paragraph explaining permanently monitored aspects by the AI
				var description = "RimGPT automatically monitors a range of essential data points to create adaptive and responsive " +
									 "personas. This includes game state, weather, colonist activities, messages, alerts, letters, and resources.\n\n" +
									 "Below you can enable additional insight feeds for more in-depth analysis:";
				listing.Label(description);

				listing.GapLine(18f);

				var yOffset = listing.CurHeight; // Current Y position after the headers and description
				var rowHeight = 60f; // Set the height for each row
				var gapBetweenRows = 10f; // Gap between rows

				// Reset font size after headers
				Text.Font = GameFont.Small;

				// Thoughts & Mood Insight settings
				DrawSettingRow(
					new Rect(inRect.x, yOffset, inRect.width, rowHeight),
					"Thoughts & Mood Insight",
					ref settings.reportColonistThoughtsFrequency,
					ref settings.reportColonistThoughts,
					ref settings.reportColonistThoughtsImmediate,
					"Enables periodic analysis by the AI of colonists' thoughts and mood impacts, as per the set frequency of in-game time."
				);
				yOffset += rowHeight + gapBetweenRows;
				// Interpersonal Insight settings
				DrawSettingRow(
					new Rect(inRect.x, yOffset, inRect.width, rowHeight),
					"Interpersonal Insight",
					ref settings.reportColonistOpinionsFrequency,
					ref settings.reportColonistOpinions,
					ref settings.reportColonistOpinionsImmediate,
					"Perioducally feeds the AI a holistic view of interpersonal dynamics and opinions periodically, based on the frequency setting of in-game time."
				);
				yOffset += rowHeight + gapBetweenRows;				
				// Power Insight settings
				DrawSettingRow(
					new Rect(inRect.x, yOffset, inRect.width, rowHeight),
					"Power Insight(Experimental)",
					ref settings.reportEnergyFrequency,
					ref settings.reportEnergyStatus,
					ref settings.reportEnergyImmediate,
					"Persistently provides the AI with detailed power grid statistics, updating at the set frequency of in-game time.\nWARNING: This embeds power data with each request. Depending on how many power generation/consumption buildings you have, this can use a lot of tokens."
				);
				yOffset += rowHeight + gapBetweenRows;
				// Research Insight settings
				DrawSettingRow(
					new Rect(inRect.x, yOffset, inRect.width, rowHeight),
					"Research Insight(Experimental)",
					ref settings.reportResearchFrequency,
					ref settings.reportResearchStatus,
					ref settings.reportResearchImmediate,
					"Pesristently provides the AI of all researched tech and progress, updating at the set frequency of in-game time.\nWARNING: This embeds research data with each request and may use a lot of tokens, depending on how many mods you have installed."
				);
				yOffset += rowHeight + gapBetweenRows;
				// Detailed Colonist Insight settings
				DrawSettingRow(
					new Rect(inRect.x, yOffset, inRect.width, rowHeight),
					"Detailed Colonist Insight(Experimental)",
					ref settings.reportColonistRosterFrequency,
					ref settings.reportColonistRoster,
					ref settings.reportColonistRosterImmediate,
					"Provides continuous updates to the AI on all colonists' details, including demographics and health. Adjust frequency (in-game time) carefully.\nWARNING: This embeds detailed colonist info with each request. This uses a lot of tokens as it sends detailed colonist info with every request to ChatGPT.  Additionally, high amount of colonists can strain CPU resources."
				);
				yOffset += rowHeight + gapBetweenRows;
				// Rooms Insight settings
				DrawSettingRow(
					new Rect(inRect.x, yOffset, inRect.width, rowHeight),
					"Rooms Insight(Experimental)",
					ref settings.reportRoomStatusFrequency,
					ref settings.reportRoomStatus,
					ref settings.reportRoomStatusImmediate,
					"Activates comprehensive room reporting to the AI, covering cleanliness, wealth, etc., at the defined frequency of in-game time\nWARNING: This sends rooms data with each request. This may use a lot of tokens, scaling with how many \"named\" rooms you currently have. Additionally, a lot of rooms may strain CPU."
				);
				DrawCloseButton(inRect);
				listing.End();
			}
			private void DrawCloseButton(Rect inRect)
			{
					// Set up the button's dimensions.
					const float buttonHeight = 40f;
					const float buttonWidth = 150f; // Or adjust to your preferred width

					// Calculate the position to center the button horizontally.
					float buttonX = (inRect.width - buttonWidth) / 2f;
					// Position the button at the bottom with a margin.
					float buttonY = inRect.yMax - buttonHeight - 10f;

					var closeButtonRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);

					// Draw the button and check for clicks.
					if (Widgets.ButtonText(closeButtonRect, "Close", true, false, true))
					{
							// If the button is clicked, close the window.
							Close();
					}
			}
			private void DrawSettingRow(Rect overallRect, string label, ref int frequencySetting, ref bool enabled, ref bool immediate, string tooltip)
			{
				var padding = 8f; // Padding between controls

				// Calculate widths for each control's Rect based on the percentage of the overallRect's width
				var sliderWidth = overallRect.width * 0.7f - padding;
				var enabledWidth = (overallRect.width * 0.15f) - padding;
				var immediateWidth = (overallRect.width * 0.15f) - padding;

				// Rect for the slider
				var sliderRect = new Rect(overallRect.x, overallRect.y, sliderWidth, overallRect.height);
				var sliderListing = new Listing_Standard();
				sliderListing.Begin(sliderRect);
				sliderListing.Label(label);
				if (enabled) {
					sliderListing.Slider(ref frequencySetting, 2500, 180000, n => $"Frequency: {FormatFrequencyLabel(n)}  ", 1, tooltip);
				}				
				sliderListing.End();

				// If the slider is disabled, overlay a 'Disabled' label
				if (!enabled)
				{
					 var disabledOverlayRect = new Rect(sliderRect.x + (sliderRect.width / 2)-16, sliderRect.y, sliderRect.width / 2-16, sliderRect.height);

					// More opaque overlay
					GUI.color = new Color(0f, 0f, 0f, 0.8f); // Dark overlay with higher opacity
					Widgets.DrawBoxSolid(disabledOverlayRect, GUI.color);
					GUI.color = Color.white; // Reset color to default
					Text.Anchor = TextAnchor.MiddleCenter; // Center text
					GUI.color = Color.red; // Text color
					Widgets.Label(disabledOverlayRect, "Disabled");
					GUI.color = Color.white; // Reset text color to default
					Text.Anchor = TextAnchor.UpperLeft; // Reset text anchor to default
				}

				// Rect for the enabled checkbox
				var enabledRect = new Rect(sliderRect.xMax + padding, overallRect.y + 16f, enabledWidth, overallRect.height);
				var enabledListing = new Listing_Standard();
				enabledListing.Begin(enabledRect);
				enabledListing.CheckboxLabeled("Enabled:", ref enabled, tooltip);
				enabledListing.End();

				// Rect for the immediate checkbox
				var immediateRect = new Rect(enabledRect.xMax + padding, overallRect.y + 16f, immediateWidth, overallRect.height);
				var immediateListing = new Listing_Standard();
				immediateListing.Begin(immediateRect);
				immediateListing.CheckboxLabeled("Immediate:", ref immediate, tooltip: "If checked, the AI will gain " + label + " immediately when starting or loading a game.");
				immediateListing.End();
			}

			private float ReadableTicks(int ticks)
			{
				const int TicksPerDay = 60000;
				const int TicksPerHour = 2500;

				if (ticks >= TicksPerDay)
				{
					return ticks / (float)TicksPerDay; // Returns days when ticks are more than or equal to a day
				}
				else
				{
					return ticks / (float)TicksPerHour / 24f; // Returns a fraction of a day when ticks are less than a day
				}
			}

			private string FormatFrequencyLabel(int ticks)
			{
				var readableValue = ReadableTicks(ticks);
				if (readableValue >= 1)
				{
					return $"{readableValue:0.#} days"; // Days
				}
				else
				{
					return $"{readableValue * 24:0.##} hrs"; // Hours (multiplied by 24 to convert from fraction of a day to hours)
				}
			}


		}


	}
}