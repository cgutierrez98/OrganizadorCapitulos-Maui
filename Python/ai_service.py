import sys
import json
import re
import argparse

# Quality/release tags to remove
QUALITY_TAGS = {
    # Resolution
    '2160p', '1080p', '720p', '480p', '4k', 'uhd', 'fhd', 'hd', 'sd',
    # HDR
    'hdr', 'hdr10', 'hdr10plus', 'dv', 'dovi', 'dolby', 'vision',
    # Source
    'web-dl', 'webdl', 'webrip', 'web', 'hdtv', 'hdtvrip', 'pdtv', 'dsr', 'dvb',
    'bluray', 'bdrip', 'brrip', 'blu-ray', 'dvdrip', 'dvdr', 'dvd',
    'cam', 'ts', 'tc', 'hdcam', 'hdts',
    # Codec
    'x264', 'x265', 'hevc', 'h264', 'h265', 'avc', 'xvid', 'divx', 'vp9', 'av1',
    # Audio
    'aac', 'ac3', 'dts', 'dts-hd', 'flac', 'mp3', 'eac3', 'atmos', 'truehd',
    'stereo', 'dual', 'multi', 'surround',
    # Language
    'spa', 'eng', 'spanish', 'english', 'latino', 'castellano',
    'sub', 'subs', 'subbed', 'engsub', 'spasub',
    # Container
    'mkv', 'mp4', 'avi', 'wmv', 'm4v',
    # Streaming
    'amzn', 'nf', 'netflix', 'dsnp', 'atvp', 'hmax', 'hulu', 'pcok', 'max',
    'amazon', 'disney', 'hbo', 'appletv',
    # Misc
    'repack', 'proper', 'real', 'rerip', 'internal', 'limited', 'unrated',
    'extended', 'complete', 'final', 'uncut', 'dc', 'imax',
}

def is_quality_word(word):
    """Check if a word is a quality/technical tag."""
    w = word.lower().strip('-_.')
    if w in QUALITY_TAGS:
        return True
    # Resolution patterns: M-1080p, 1080pDUAL, etc.
    if re.match(r'^m?-?\d+p', w, re.IGNORECASE):
        return True
    # Audio: 5.1, 7.1
    if re.match(r'^\d\.\d$', w):
        return True
    # Combined tags: Spa-Eng, Spa-EngSub
    if re.match(r'^[a-z]{2,4}-[a-z]{2,6}(sub)?$', w, re.IGNORECASE):
        return True
    return False

def clean_series_name(text):
    """Clean series name, being less aggressive to preserve important words."""
    if not text:
        return ""
    
    # Remove brackets
    text = re.sub(r'\[.*?\]', '', text)
    text = re.sub(r'\(.*?\)', '', text)
    
    # Replace underscores/dots with spaces
    text = text.replace('_', ' ')
    text = re.sub(r'\.', ' ', text)
    
    # Split and filter
    words = text.split()
    clean_words = [w.strip('-_.') for w in words if w.strip('-_.') and not is_quality_word(w)]
    
    # Remove trailing years
    result = ' '.join(clean_words)
    result = re.sub(r'\s+(?:19|20)\d{2}\s*$', '', result)
    
    return result.strip()

def clean_episode_title(text):
    """Clean episode title, being more aggressive with quality tags."""
    if not text:
        return ""
    
    # Remove brackets
    text = re.sub(r'\[.*?\]', '', text)
    text = re.sub(r'\(.*?\)', '', text)
    
    # Replace underscores/dots with spaces
    text = text.replace('_', ' ')
    text = re.sub(r'\.', ' ', text)
    
    # Split into words
    words = text.split()
    clean_words = []
    
    # Stop when we hit quality tags
    for word in words:
        w = word.strip('-_.')
        if not w:
            continue
        if is_quality_word(w):
            break  # Stop at first quality tag
        clean_words.append(w)
    
    return ' '.join(clean_words).strip()

def analyze_filename(filename):
    """
    Analyzes a filename to extract series, season, episode, and title.
    """
    result = {
        "series": "",
        "season": 0,
        "episode": 0,
        "title": "",
        "original": filename
    }
    
    # Remove extension
    name = re.sub(r'\.(mkv|mp4|avi|wmv|mov|flv|webm)$', '', filename, flags=re.IGNORECASE)
    
    # Patterns
    patterns = [
        # S01E01 format
        r"(?P<series>.+?)[.\s_-]+[Ss](?P<season>\d{1,2})[.\s_-]?[Ee](?P<episode>\d{1,3})(?:[.\s_-]+(?P<title>.+))?",
        # 1x01 format
        r"(?P<series>.+?)[_.\s-]+(?P<season>\d{1,2})x(?P<episode>\d{2,3})(?:[_.\s-]+(?P<title>.+))?",
        # Season X Episode X
        r"(?P<series>.+?)[.\s_-]+Season[.\s_-]?(?P<season>\d{1,2})[.\s_-]+Episode[.\s_-]?(?P<episode>\d{1,3})(?:[.\s_-]+(?P<title>.+))?",
        # Spanish: Temporada/Capitulo
        r"(?P<series>.+?)[.\s_-]+(?:Temp|Temporada)[.\s_-]?(?P<season>\d{1,2})[.\s_-]+(?:Cap|Capitulo)[.\s_-]?(?P<episode>\d{1,3})(?:[.\s_-]+(?P<title>.+))?",
    ]
    
    for pattern in patterns:
        match = re.search(pattern, name, re.IGNORECASE)
        if match:
            groups = match.groupdict()
            result["series"] = clean_series_name(groups.get("series", ""))
            result["season"] = int(groups.get("season", 0))
            result["episode"] = int(groups.get("episode", 0))
            
            raw_title = groups.get("title", "") or ""
            result["title"] = clean_episode_title(raw_title)
            
            if result["series"] and result["episode"] > 0:
                return result
    
    # Anime fallback (absolute numbering)
    anime_match = re.search(r"(?P<series>.+?)[.\s_-]+(?P<episode>\d{2,4})(?:[.\s_-]+(?P<title>.+))?$", name)
    if anime_match:
        groups = anime_match.groupdict()
        series = clean_series_name(groups.get("series", ""))
        if len(series) > 2:
            result["series"] = series
            result["season"] = 1
            result["episode"] = int(groups.get("episode", 0))
            result["title"] = clean_episode_title(groups.get("title", "") or "")
    
    return result

def normalize_title(text):
    return clean_series_name(text)

def main():
    parser = argparse.ArgumentParser(description="AI Service for File Organizer")
    parser.add_argument("command", choices=["analyze", "normalize"])
    parser.add_argument("--input", required=True)
    
    args = parser.parse_args()
    
    try:
        if args.command == "analyze":
            output = analyze_filename(args.input)
        else:
            output = {"result": normalize_title(args.input)}
        print(json.dumps(output, ensure_ascii=False))
    except Exception as e:
        print(json.dumps({"error": str(e)}))
        sys.exit(1)

if __name__ == "__main__":
    main()
