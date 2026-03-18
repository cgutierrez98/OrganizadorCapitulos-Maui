import sys
import os
import pytest

# Allow importing ai_service from project Python folder
this_dir = os.path.dirname(__file__)
project_dir = os.path.abspath(os.path.join(this_dir, '..'))
if project_dir not in sys.path:
    sys.path.insert(0, project_dir)

from ai_service import analyze_filename


def test_s01e01_format():
    res = analyze_filename('Show.Name.S01E02.Title.mkv')
    assert isinstance(res, dict)
    assert res['season'] == 1
    assert res['episode'] == 2
    assert 'Show' in res['series'] or len(res['series']) > 0


def test_1x01_format():
    res = analyze_filename('Series_Name 2x05 Episode.mkv')
    assert res['season'] == 2
    assert res['episode'] == 5


def test_anime_fallback():
    res = analyze_filename('GreatAnime.012.mkv')
    assert res['season'] == 1
    assert res['episode'] == 12
    assert len(res['series']) > 0


def test_noisy_name():
    res = analyze_filename('My.Show.1080p.Web-DL.x264.Eng.S02E03.mkv')
    assert res['season'] == 2
    assert res['episode'] == 3
    assert 'My Show' in res['series'] or len(res['series']) > 0
