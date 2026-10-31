using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

// Modelos do payload "play-config" do site emuseu (React -> Unity).
// Usados com Newtonsoft: chaves nulas ou que faltam sao ignoradas (ficam null/0).
// Prefixo "Emuseu" para nao colidir com outras classes do projeto.
// [Preserve] evita o IL2CPP (WebGL) cortar campos que so sao usados por reflexao.

[Preserve]
public class EmuseuEnvelope
{
    public int version;
    public string channel;
    public string type;
    public EmuseuPayload payload;
}

[Preserve]
public class EmuseuPayload
{
    public EmuseuUser user;
    public EmuseuCampaign campaign;
    public List<JToken> sponsors;   // ainda nao usado (proximo passo)
}

[Preserve]
public class EmuseuUser
{
    public string id;
    public List<string> roles;
}

[Preserve]
public class EmuseuCampaign
{
    public string id;
    public string name;
    public List<EmuseuGame> games;
    public List<EmuseuAlbum> albums;
}

[Preserve]
public class EmuseuGame
{
    public string id;
    public string name;
    public string description;
    public string imageUrl;
    public List<EmuseuSetting> settings;   // ex: key "outdoor_1" (banners - proximo passo)
}

[Preserve]
public class EmuseuSetting
{
    public string id;
    public string alias;
    public string key;
    public string type;
    public JToken value;
    public EmuseuFile file;
}

[Preserve]
public class EmuseuFile
{
    public string url;
    public string contentType;
    public float width;
    public float height;
}

[Preserve]
public class EmuseuAlbum
{
    public string id;
    public string name;
    public string description;
    public bool active;
    public List<EmuseuPage> pages;
}

[Preserve]
public class EmuseuPage
{
    public string id;
    public string title;
    public string titleEn;
    public string city;
    public string cityEn;
    public List<EmuseuCard> cards;
}

[Preserve]
public class EmuseuCard
{
    public string id;
    public int number;
    public string title;
    public string titleEn;
    public string description;
    public string descriptionEn;
    public string frame;        // nosso id do Final Card, ex: "F11"
    public string border;       // common | host | special
    public EmuseuRelease release;
    public string imageUrl;     // URLs assinadas: podem expirar
    public string thumbUrl;
    public string videoUrl;
    public string media;        // "image" ou "video"
}

[Preserve]
public class EmuseuRelease
{
    public string mode;         // closed | immediate | ...
    public string at;
}