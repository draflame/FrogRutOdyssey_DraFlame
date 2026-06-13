/// <summary>
/// Tham chiếu đến một tile trong TilePack — thay thế TileType enum trong map data.
/// Lưu bằng string để dễ đọc và không phụ thuộc vào enum cứng.
/// </summary>
[System.Serializable]
public struct TileRef
{
    [UnityEngine.Tooltip("Tên pack chứa tile này. Phải khớp với TilePack.packName.")]
    public string packName;

    [UnityEngine.Tooltip("ID của tile trong pack đó. Phải khớp với TilePackEntry.id.")]
    public string tileId;

    // ──────────────────────────────────────────────────
    //  Factories & Helpers
    // ──────────────────────────────────────────────────

    public static readonly TileRef Empty = new TileRef { packName = "", tileId = "" };

    public bool IsEmpty => string.IsNullOrEmpty(tileId);

    public TileRef(string packName, string tileId)
    {
        this.packName = packName;
        this.tileId = tileId;
    }

    public override string ToString() => IsEmpty ? "[Empty]" : $"[{packName}/{tileId}]";

    public override bool Equals(object obj)
    {
        if (obj is TileRef other)
            return packName == other.packName && tileId == other.tileId;
        return false;
    }

    public override int GetHashCode()
    {
        return (packName ?? "").GetHashCode() ^ ((tileId ?? "").GetHashCode() << 16);
    }

    public static bool operator ==(TileRef a, TileRef b)
        => a.packName == b.packName && a.tileId == b.tileId;

    public static bool operator !=(TileRef a, TileRef b) => !(a == b);
}
