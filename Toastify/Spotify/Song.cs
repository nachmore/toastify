namespace Toastify
{
  public class Song
  {
    /// <summary>
    /// Is this a real Song or is Spotify not playing anything?
    ///
    /// Note: Window title doesn't change regardless of UI language
    /// </summary>
    public bool IsValid =>
      (!string.IsNullOrEmpty(Artist) && Artist != "Spotify Free" && Artist != "Spotify Premium") ||
      (!string.IsNullOrEmpty(Track));

    public string Artist { get; set; }
    public string Track { get; set; }
    public string Album { get; set; }

    public string CoverArtUrl { get; set; }

    public Song(string artist, string title, string album = null)
    {
      Artist = artist;
      Track = title;
      Album = album;
    }

    public override string ToString()
    {
      if (Artist == null)
        return Track;

      return string.Format("{0} - {1}", Artist, Track);
    }

    public override bool Equals(object obj)
    {
      if (!(obj is Song target))
        return false;

      return (target.Artist == this.Artist && target.Track == this.Track);
    }

    // overriding GetHashCode is "required" when overriding Equals
    public override int GetHashCode()
    {
      return base.GetHashCode();
    }
  }
}
