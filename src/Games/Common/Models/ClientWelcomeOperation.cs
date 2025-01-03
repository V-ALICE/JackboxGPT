﻿using Newtonsoft.Json;

namespace JackboxGPT.Games.Common.Models
{
    public struct ClientWelcome
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}