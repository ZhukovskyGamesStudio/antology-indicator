#!/usr/bin/env python3
"""
Пересобирает таблицу кернинга TMP-ассета шрифта из исходного OTF/TTF.

Зачем: нативный font engine Unity (FontEngine.GetPairAdjustmentRecords) неверно
разбирает GPOS-кернинг формата 2 (class-based PairPos) у Old-Soviet.otf. В ассет
попадает мусор: индексы глифов вне диапазона шрифта, положительные подвижки и
подвижки на парах, которых в шрифте нет вообще. Русский текст от этого слипается.

Скрипт читает настоящий kern из шрифта через fontTools и пишет его в .asset
в том же формате, в каком его пишет Font Asset Creator:
    m_XAdvance = значение_в_юнитах * (m_PointSize / m_UnitsPerEM)
Заодно чистит m_LigatureSubstitutionRecords, если в шрифте нет GSUB-фич.

Запуск (из корня проекта):
    python Tools/fix_tmp_kerning.py

Нужен fonttools:  pip install fonttools

ВАЖНО: если перегенерировать ассет шрифта через Window → TextMeshPro → Font Asset
Creator с включённым "Include Font Features", мусор вернётся — прогони скрипт снова.
"""

import os
import re
import sys

try:
    from fontTools.ttLib import TTFont
except ImportError:
    sys.exit("Нужен fonttools:  pip install fonttools")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

TARGETS = [
    (
        os.path.join(ROOT, "Assets", "Fonts", "Old Soviet", "Old-Soviet.asset"),
        os.path.join(ROOT, "Assets", "Fonts", "Old Soviet", "Old-Soviet.otf"),
    ),
]


def read_kern_pairs(font):
    """{(имя_глифа_1, имя_глифа_2): подвижка_в_юнитах} из GPOS-фичи kern."""
    pairs = {}
    if "GPOS" not in font:
        return pairs
    glyph_order = font.getGlyphOrder()
    gpos = font["GPOS"].table

    def put(a, b, value):
        # Внутри одного lookup выигрывает первый подходящий subtable,
        # поэтому точечные пары (формат 1) перекрывают классовые (формат 2).
        pairs.setdefault((a, b), value)

    for record in gpos.FeatureList.FeatureRecord:
        if record.FeatureTag != "kern":
            continue
        for lookup_index in record.Feature.LookupListIndex:
            lookup = gpos.LookupList.Lookup[lookup_index]
            if lookup.LookupType != 2:
                continue
            for sub in lookup.SubTable:
                if sub.Format == 1:
                    for i, first in enumerate(sub.Coverage.glyphs):
                        for pair in sub.PairSet[i].PairValueRecord:
                            put(first, pair.SecondGlyph, getattr(pair.Value1, "XAdvance", 0) or 0)
                elif sub.Format == 2:
                    class1 = {}
                    for glyph in sub.Coverage.glyphs:
                        class1.setdefault(sub.ClassDef1.classDefs.get(glyph, 0), []).append(glyph)
                    class2 = {}
                    for glyph in glyph_order:
                        class2.setdefault(sub.ClassDef2.classDefs.get(glyph, 0), []).append(glyph)
                    for i, row in enumerate(sub.Class1Record):
                        for j, cell in enumerate(row.Class2Record):
                            value = getattr(cell.Value1, "XAdvance", 0) or 0
                            for a in class1.get(i, ()):
                                for b in class2.get(j, ()):
                                    put(a, b, value)
    return {k: v for k, v in pairs.items() if v}


def has_gsub_features(font):
    return "GSUB" in font and bool(font["GSUB"].table.FeatureList.FeatureRecord)


def block_bounds(text, key, next_key):
    """Границы YAML-блока от строки key до строки next_key."""
    start = text.index(key)
    end = text.index(next_key, start)
    return start, end


def format_value(value):
    text = "%.6g" % value
    return "0" if text in ("-0", "0") else text


def fix(asset_path, font_path):
    name = os.path.basename(asset_path)
    with open(asset_path, "rb") as handle:
        raw = handle.read()
    text = raw.decode("utf-8")
    newline = "\r\n" if "\r\n" in text[:4000] else "\n"

    point_size = float(re.search(r"    m_PointSize: ([-\d.e]+)", text).group(1))
    units_per_em = float(re.search(r"    m_UnitsPerEM: ([-\d.e]+)", text).group(1))
    em_scale = point_size / units_per_em

    char_table = text[block_bounds(text, "  m_CharacterTable:", "  m_AtlasTextures:")[0]:
                      block_bounds(text, "  m_CharacterTable:", "  m_AtlasTextures:")[1]]
    asset_glyphs = {
        int(m.group(2)) for m in re.finditer(r"m_Unicode: (\d+)\s*\n\s*m_GlyphIndex: (\d+)", char_table)
    }

    font = TTFont(font_path)
    glyph_order = font.getGlyphOrder()
    index_of = {glyph_name: i for i, glyph_name in enumerate(glyph_order)}
    cmap = font.getBestCmap()

    # Индексы глифов в ассете обязаны совпадать с порядком глифов в шрифте,
    # иначе кернинг ляжет не на те символы.
    for unicode_value, glyph_index in re.findall(r"m_Unicode: (\d+)\s*\n\s*m_GlyphIndex: (\d+)", char_table):
        glyph_name = cmap.get(int(unicode_value))
        if glyph_name is not None and index_of.get(glyph_name) != int(glyph_index):
            sys.exit("%s: индексы глифов разошлись со шрифтом — ассет надо перегенерировать" % name)

    pairs = read_kern_pairs(font)
    records = []
    for (first, second), units in pairs.items():
        a, b = index_of.get(first), index_of.get(second)
        if a is None or b is None or a not in asset_glyphs or b not in asset_glyphs:
            continue
        records.append((a, b, units * em_scale))
    records.sort()

    lines = ["    m_GlyphPairAdjustmentRecords:"]
    for a, b, advance in records:
        lines += [
            "    - m_FirstAdjustmentRecord:",
            "        m_GlyphIndex: %d" % a,
            "        m_GlyphValueRecord:",
            "          m_XPlacement: 0",
            "          m_YPlacement: 0",
            "          m_XAdvance: %s" % format_value(advance),
            "          m_YAdvance: 0",
            "      m_SecondAdjustmentRecord:",
            "        m_GlyphIndex: %d" % b,
            "        m_GlyphValueRecord:",
            "          m_XPlacement: 0",
            "          m_YPlacement: 0",
            "          m_XAdvance: 0",
            "          m_YAdvance: 0",
            "      m_FeatureLookupFlags: 0",
        ]
    kern_block = newline.join(lines) + newline

    start, end = block_bounds(text, "    m_GlyphPairAdjustmentRecords:", "    m_MarkToBaseAdjustmentRecords:")
    old_count = text[start:end].count("m_FirstAdjustmentRecord:")
    text = text[:start] + kern_block + text[end:]

    dropped_ligatures = 0
    if "    m_LigatureSubstitutionRecords:" in text and not has_gsub_features(font):
        start, end = block_bounds(text, "    m_LigatureSubstitutionRecords:", "    m_GlyphPairAdjustmentRecords:")
        dropped_ligatures = text[start:end].count("m_LigatureGlyphID:")
        text = text[:start] + "    m_LigatureSubstitutionRecords: []" + newline + text[end:]

    with open(asset_path, "wb") as handle:
        handle.write(text.encode("utf-8"))

    print("%s: кернинг %d -> %d пар (emScale %.4f)" % (name, old_count, len(records), em_scale))
    if dropped_ligatures:
        print("%s: убрано %d фиктивных лигатур (в шрифте нет GSUB-фич)" % (name, dropped_ligatures))


if __name__ == "__main__":
    for asset, source in TARGETS:
        fix(asset, source)
