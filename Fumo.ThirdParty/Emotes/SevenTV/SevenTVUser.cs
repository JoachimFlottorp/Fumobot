﻿using System.Text.Json.Serialization;

namespace Fumo.ThirdParty.Emotes.SevenTV;

public record SevenTVConnection(
[property: JsonPropertyName("id")] string Id,
[property: JsonPropertyName("platform")] string Platform,
[property: JsonPropertyName("emote_set_id")] string EmoteSetId
);

public record OuterSevenTVUser(
[property: JsonPropertyName("userByConnection")] SevenTVUser UserByConnection
);

public record SevenTVUserEmote(
[property: JsonPropertyName("id")] string Id
);

public record SevenTVUserEmoteSet(
[property: JsonPropertyName("id")] string Id,
[property: JsonPropertyName("emotes")] IReadOnlyList<SevenTVUserEmote> Emotes,
[property: JsonPropertyName("capacity")] int Capacity
);

public record SevenTVUser(
[property: JsonPropertyName("id")] string Id,
[property: JsonPropertyName("type")] string Type,
[property: JsonPropertyName("username")] string Username,
[property: JsonPropertyName("roles")] IReadOnlyList<string> Roles,
[property: JsonPropertyName("created_at")] DateTime CreatedAt,
[property: JsonPropertyName("connections")] IReadOnlyList<SevenTVConnection> Connections,
[property: JsonPropertyName("emote_sets")] IReadOnlyList<SevenTVUserEmoteSet> EmoteSets
)
{
    public SevenTVUserEmoteSet DefaultEmoteSet()
    {
        var id = Connections.First(x => x.Platform == "TWITCH").EmoteSetId;

        return EmoteSets.First(x => x.Id == id);
    }
}

