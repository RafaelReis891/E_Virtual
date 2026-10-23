using System;
using System.Collections.Generic;

// Estrutura igual ao cards.json. Campos EN e video sao opcionais.
[Serializable]
public class FinalCardData
{
    public string id;
    public string filename;
    public string orientation;
    public string title;
    public string description;
    public string shortDescription;
    public string image;
    public string imagePath;

    // Opcionais (adicione no json quando tiver):
    public string titleEN;
    public string descriptionEN;
    public string video;      // ex: "StreamingAssets/Videos/F11.mp4"
}

[Serializable]
public class FinalCardAlbum
{
    public string album;
    public string startId;
    public string endId;
    public List<FinalCardData> cards = new List<FinalCardData>();

    public FinalCardData Buscar(string id)
    {
        if (cards == null) return null;
        foreach (var c in cards) if (c != null && c.id == id) return c;
        return null;
    }
}
