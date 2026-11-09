"""Generates Pulsewave's built-in theme templates (src/Pulsewave/Templates/*.json).

The JSON files are what ship; this script only makes them easier to adjust consistently.
Run: python scripts/build-templates.py   (then build Pulsewave and run the regression checks,
which include the template quality gate: fonts, on-screen, no text overlap, contrast).
Coordinates are display pixels (1920x480 unless a template sets another screen size);
font sizes are points (px = pt * 4/3).
"""
import json
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'Pulsewave', 'Templates')

# Segoe Fluent Icons (built into Windows 11; Segoe MDL2 Assets on Windows 10 has the same code points).
ICON = {'cpu': '', 'gpu': '', 'temp': '', 'power': '', 'pulse': '', 'game': '',
        'down': '', 'up': '', 'clock': '', 'calendar': '', 'network': ''}


def panel(x, y, w, h, fill, gradient=None, angle=90, style='Linear', border=None, border_width=0, radius=16, corners='Round', glow=None, glow_size=12):
    e = dict(Kind='Panel', X=x, Y=y, Width=w, Height=h, FillColor=fill, GradientColor=gradient, GradientAngle=angle, GradientStyle=style,
             BorderColor=border or '#00000000', BorderWidth=border_width, CornerRadius=radius, CornerStyle=corners)
    return glowing(e, glow, glow_size)


def text(x, y, s, size, color, family='Segoe UI', bold=False, align='Left', glow=None, glow_size=12):
    return glowing(dict(Kind='Text', X=x, Y=y, Text=s, Font=dict(Family=family, Size=size, Bold=bold, Color=color, Alignment=align)), glow, glow_size)


def icon(x, y, name, size, color, align='Left', glow=None):
    return text(x, y, ICON[name], size, color, family='Segoe Fluent Icons', align=align, glow=glow)


def value(x, y, data, size, color, family='Segoe UI', bold=False, align='Left', unit=True, fmt=None, glow=None, glow_size=12, width=None):
    e = dict(Kind='Data', X=x, Y=y, DataName=data, ShowUnit=unit, Font=dict(Family=family, Size=size, Bold=bold, Color=color, Alignment=align))
    if fmt: e['DataFormat'] = fmt
    if width: e['Width'] = width  # longer text ends in an ellipsis
    return glowing(e, glow, glow_size)


def bar(x, y, w, h, data, fore, back, radius=0, gradient=None, segment=0, gap=0, maximum=100, glow=None):
    return glowing(dict(Kind='Bar', X=x, Y=y, Width=w, Height=h, DataName=data, ForeColor=fore, BackColor=back, CornerRadius=radius,
                        GradientColor=gradient, SegmentWidth=segment, SegmentGap=gap, Maximum=maximum), glow, 10)


def gauge(cx, cy, diameter, thickness, data, fore, back, start=135, sweep=270, round_caps=True, gradient=None, segment=0, gap=0, maximum=100, glow=None):
    r = diameter / 2
    return glowing(dict(Kind='Arc', X=cx - r, Y=cy - r, Width=diameter, Height=diameter, Diameter=diameter, Thickness=thickness, DataName=data,
                        ForeColor=fore, BackColor=back, StartAngle=start, SweepAngle=sweep, RoundCaps=round_caps, GradientColor=gradient,
                        SegmentWidth=segment, SegmentGap=gap, Maximum=maximum), glow, 14)


def chart(x, y, w, h, data, line, fill, width=2, samples=60, maximum=100, border=None, border_width=0):
    return dict(Kind='Chart', X=x, Y=y, Width=w, Height=h, DataName=data, LineColor=line, FillColor=fill, LineWidth=width, Samples=samples,
                Maximum=maximum, BorderColor=border or '#00000000', BorderWidth=border_width)


def circle(cx, cy, diameter, fill, gradient=None, border=None, border_width=0):
    r = diameter / 2
    return panel(cx - r, cy - r, diameter, diameter, fill, gradient, style='Radial', border=border, border_width=border_width, radius=r)


def album_art(x, y, size, radius=12, fill='#FF1E2633', note='#FF8FA3B8'):
    return dict(Kind='AlbumArt', X=x, Y=y, Width=size, Height=size, CornerRadius=radius, FillColor=fill, LineColor=note)


def now_playing(x, y, art, text_width, accent, title_color, artist_color, title_size=16, artist_size=13, family='Segoe UI'):
    """Cover art with state, title and artist beside it; title and artist are capped to text_width."""
    tx = x + art + 18
    return [album_art(x, y, art),
            value(tx, y, 'MEDIA_STATE', 10, accent, family, True, unit=False),
            value(tx, y + art * 0.26, 'MEDIA_TITLE', title_size, title_color, family, True, unit=False, width=text_width),
            value(tx, y + art * 0.62, 'MEDIA_ARTIST', artist_size, artist_color, family, unit=False, width=text_width)]


def line(x1, y1, x2, y2, color, width=1.5, dash=0, gap=0, glow=None):
    return glowing(dict(Kind='Line', X=x1, Y=y1, Width=x2 - x1, Height=y2 - y1, LineColor=color, LineWidth=width, SegmentWidth=dash, SegmentGap=gap), glow, 8)


def glowing(e, glow, size):
    if glow:
        e['GlowColor'] = glow
        e['GlowSize'] = size
    return e


def tap(e, action):
    """What tapping the element does (click-through off): PlayPause, NextTrack, PreviousTrack, Details, NextTheme, PreviousTheme, Launch, AppDrawer."""
    e['Action'] = action
    return e


PREVIOUS_GLYPH, NEXT_GLYPH = '', ''


def media_buttons(cx, cy, d, gap, color, back, border):
    """Previous, play/pause (its icon follows the player) and next as round touch buttons centred on cx, cy."""
    pt = round(d * 0.34 * 0.75)
    px = pt * 4 / 3
    out = []
    for dx, action in [(-(d + gap), 'PreviousTrack'), (0, 'PlayPause'), (d + gap, 'NextTrack')]:
        x = cx + dx
        out.append(tap(circle(x, cy, d, back, border=border, border_width=2), action))
        if action == 'PlayPause':
            out.append(tap(value(x, cy - px * 0.5, 'MEDIA_PLAYPAUSE', pt, color, 'Segoe Fluent Icons', align='Center', unit=False), action))
        else:
            glyph = PREVIOUS_GLYPH if action == 'PreviousTrack' else NEXT_GLYPH
            out.append(tap(text(x, cy - px * 0.5, glyph, pt, color, 'Segoe Fluent Icons', align='Center'), action))
    return out


def theme(name, background, elements, width=1920, height=480):
    return dict(Format=1, Name=name, Source='Pulsewave template', Width=width, Height=height, BackgroundColor=background,
                Elements=[{k: v for k, v in e.items() if v is not None} for e in elements])


# --- Clean cards ----------------------------------------------------------------------------

def glass_cards():
    label, dim, white = '#FF9FB3C8', '#FFB8C7D9', '#FFF4F8FC'
    cpu, gpu, sys = '#FF5EE6D0', '#FF8AB4FF', '#FFC9A7FF'
    card = dict(fill='#F21A2435', gradient='#F2111926', border='#2EFFFFFF', border_width=1.5, radius=24)
    e = [panel(0, 0, 1920, 480, '#FF1B2740', '#FF06090F', style='Radial', radius=0)]
    # Time
    e += [panel(20, 20, 400, 440, **card),
          icon(48, 46, 'clock', 16, label), text(84, 44, 'LOCAL TIME', 14, label, 'Segoe UI', True),
          value(220, 84, 'TIME', 84, white, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(206, 240, 'DAY', 17, dim, 'Segoe UI', True, align='Right'),
          value(220, 240, 'DATE', 17, dim, 'Segoe UI'),
          line(48, 300, 392, 300, '#26FFFFFF')]
    e += now_playing(48, 330, 80, 236, cpu, white, dim)
    # CPU and GPU
    for x, accent, kind, model, load, temp, extra, extra_label, extra_icon in [
            (436, cpu, 'CPU', 'CPUMODEL', 'CPULOAD', 'CPUTEMP', 'CPUPWR', 'POWER', 'power'),
            (940, gpu, 'GPU', 'GPUMODEL', 'GPULOAD', 'GPUTEMP', 'FPS', 'FPS', 'game')]:
        e += [panel(x, 20, 488, 440, **card),
              icon(x + 28, 46, 'cpu' if kind == 'CPU' else 'gpu', 16, accent), text(x + 64, 44, kind, 14, accent, 'Segoe UI', True),
              value(x + 120, 47, model, 12, dim, 'Segoe UI', unit=False),
              value(x + 26, 86, load, 64, white, 'Segoe UI Semibold'),
              text(x + 462, 98, 'TEMP', 12, label, 'Segoe UI', True, 'Right'),
              value(x + 462, 120, temp, 30, accent, 'Segoe UI Semibold', align='Right'),
              text(x + 462, 186, extra_label, 12, label, 'Segoe UI', True, 'Right'),
              value(x + 462, 208, extra, 22, white, 'Segoe UI Semibold', align='Right', unit=extra != 'FPS'),
              text(x + 28, 262, 'LOAD · 60 s', 11, label, 'Segoe UI', True),
              chart(x + 28, 288, 432, 146, load, accent, accent.replace('#FF', '#33'), 2.5)]
    # System
    x = 1444
    e += [panel(x, 20, 456, 440, **card),
          icon(x + 28, 46, 'pulse', 16, sys), text(x + 64, 44, 'SYSTEM', 14, sys, 'Segoe UI', True),
          text(x + 28, 100, 'MEMORY', 12, label, 'Segoe UI', True),
          value(x + 428, 84, 'RAMLOAD', 40, white, 'Segoe UI Semibold', align='Right'),
          bar(x + 28, 150, 400, 12, 'RAMLOAD', sys, '#33FFFFFF', radius=6),
          value(x + 28, 172, 'RAM_GB', 16, dim, 'Segoe UI'), text(x + 428, 174, 'IN USE', 11, label, 'Segoe UI', True, 'Right'),
          line(x + 28, 228, x + 428, 228, '#26FFFFFF'),
          icon(x + 28, 258, 'down', 18, cpu), text(x + 64, 260, 'DOWNLOAD', 12, label, 'Segoe UI', True),
          value(x + 428, 250, 'DOWNDSPEED', 26, white, 'Segoe UI Semibold', align='Right'),
          icon(x + 28, 338, 'up', 18, gpu), text(x + 64, 340, 'UPLOAD', 12, label, 'Segoe UI', True),
          value(x + 428, 330, 'UPSPEED', 26, white, 'Segoe UI Semibold', align='Right')]
    return theme('Glass Cards', '#FF06090F', e)


def aurora():
    white, soft = '#FFFFFFFF', '#FFE3E9F5'
    card = dict(fill='#8C0A0E1C', border='#40FFFFFF', border_width=1.5, radius=28)
    e = [panel(0, 0, 1920, 480, '#FF3A1E6E', '#FF0B5563', angle=15, radius=0),
         panel(1150, -200, 900, 700, '#3326E0C8', '#0026E0C8', style='Radial', radius=0)]
    for x, title, icon_name, load, temp, accent in [(24, 'PROCESSOR', 'cpu', 'CPULOAD', 'CPUTEMP', '#FF7CF2E0'),
                                                     (648, 'GRAPHICS', 'gpu', 'GPULOAD', 'GPUTEMP', '#FFB79CFF')]:
        e += [panel(x, 24, 600, 432, **card),
              icon(x + 36, 56, icon_name, 18, accent), text(x + 76, 54, title, 15, soft, 'Segoe UI', True),
              value(x + 36, 104, load, 90, white, 'Segoe UI Light'),
              text(x + 564, 120, 'TEMPERATURE', 12, soft, 'Segoe UI', True, 'Right'),
              value(x + 564, 144, temp, 40, white, 'Segoe UI Semibold', align='Right'),
              bar(x + 36, 360, 528, 14, load, accent, '#33FFFFFF', radius=7, gradient='#FFFFFFFF'),
              text(x + 36, 392, 'LOAD', 12, soft, 'Segoe UI', True)]
    e += [text(476, 392, 'POWER', 12, soft, 'Segoe UI', True, 'Right'), value(588, 388, 'CPUPWR', 15, white, 'Segoe UI', True, 'Right'),
          text(1100, 392, 'FPS', 12, soft, 'Segoe UI', True, 'Right'), value(1212, 388, 'FPS', 15, white, 'Segoe UI', True, 'Right', unit=False)]
    x = 1272
    e += [panel(x, 24, 624, 432, **card),
          icon(x + 36, 56, 'clock', 18, '#FF7CF2E0'), text(x + 76, 54, 'NOW', 15, soft, 'Segoe UI', True),
          value(x + 312, 104, 'TIME', 100, white, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(x + 312, 300, 'DAY', 24, soft, 'Segoe UI', True, 'Center'),
          value(x + 312, 344, 'DATE', 22, soft, 'Segoe UI', align='Center')]
    return theme('Aurora', '#FF1B2440', e)


# --- Sci-fi / HUD -----------------------------------------------------------------------------

def vector_hud():
    cyan, white, dim = '#FF35D9FF', '#FFEAFBFF', '#FF8FD9EE'
    glow = '#9935D9FF'
    frame = dict(fill='#E6031018', border=cyan, border_width=2, radius=22, corners='Cut', glow=glow, glow_size=16)
    e = [panel(0, 0, 1920, 480, '#FF041019', '#FF010407', style='Radial', radius=0)]
    e += [line(x, 0, x, 480, '#2635D9FF', 1, 4, 8) for x in range(120, 1920, 120)]
    e += [line(0, y, 1920, y, '#2035D9FF', 1, 4, 8) for y in (120, 240, 360)]
    for x, name, load, temp, power, tag in [(24, 'CPU', 'CPULOAD', 'CPUTEMP', 'CPUPWR', 'CORE'), (1336, 'GPU', 'GPULOAD', 'GPUTEMP', 'GPUPWR', 'RENDER')]:
        e += [panel(x, 24, 560, 432, **frame),
              text(x + 34, 44, f'{name} // {tag}', 15, cyan, 'Bahnschrift SemiBold'),
              line(x + 34, 78, x + 526, 78, '#6635D9FF', 1),
              text(x + 34, 100, 'UTILIZATION', 12, dim, 'Bahnschrift'),
              value(x + 34, 118, load, 72, white, 'Bahnschrift SemiBold', glow='#6635D9FF', glow_size=18),
              bar(x + 34, 238, 492, 18, load, cyan, '#2235D9FF', segment=10, gap=5, glow=glow),
              text(x + 34, 290, 'TEMP', 12, dim, 'Bahnschrift'), value(x + 34, 310, temp, 34, white, 'Bahnschrift SemiBold'),
              text(x + 300, 290, 'POWER', 12, dim, 'Bahnschrift'), value(x + 300, 310, power, 34, white, 'Bahnschrift SemiBold'),
              text(x + 34, 392, 'STATUS', 11, dim, 'Bahnschrift'), text(x + 120, 392, 'NOMINAL', 11, cyan, 'Bahnschrift SemiBold')]
    x = 600
    e += [panel(x, 24, 720, 432, **frame),
          line(x + 30, 60, x + 90, 60, cyan, 2), line(x + 630, 60, x + 690, 60, cyan, 2),
          text(x + 360, 44, 'SYSTEM CLOCK', 15, cyan, 'Bahnschrift SemiBold', align='Center'),
          value(x + 360, 82, 'TIME', 104, cyan, 'Bahnschrift Light', align='Center', fmt='h:m:s', glow=glow, glow_size=22),
          value(x + 360, 236, 'DATE', 20, dim, 'Bahnschrift', align='Center'),
          line(x + 40, 290, x + 680, 290, '#4435D9FF', 1, 6, 6),
          text(x + 60, 312, 'FRAMERATE', 12, dim, 'Bahnschrift'), value(x + 60, 332, 'FPS', 40, white, 'Bahnschrift SemiBold', unit=False),
          text(x + 360, 312, 'MEMORY', 12, dim, 'Bahnschrift', align='Center'), value(x + 360, 332, 'RAMLOAD', 40, white, 'Bahnschrift SemiBold', align='Center'),
          text(x + 660, 312, 'NETWORK', 12, dim, 'Bahnschrift', align='Right'), value(x + 660, 340, 'DOWNDSPEED', 28, white, 'Bahnschrift SemiBold', align='Right')]
    return theme('Vector HUD', '#FF010407', e)


def ember_ops():
    orange, cream, dim = '#FFFF9A3C', '#FFFFE6CC', '#FFD9A574'
    glow = '#AAFF8A1F'
    module = dict(fill='#F21C1712', gradient='#F2120F0C', border='#CCFF9A3C', border_width=2, radius=18, corners='Cut')
    e = [panel(0, 0, 1920, 480, '#FF1A1510', '#FF080706', angle=90, radius=0),
         panel(0, 0, 1920, 52, '#FF231B12', border='#66FF9A3C', border_width=1, radius=0),
         text(28, 12, 'SYSTEM STATUS', 17, orange, 'Bahnschrift Bold'),
         line(250, 26, 1560, 26, '#55FF9A3C', 2, 14, 8),
         value(1892, 10, 'TIME', 20, cream, 'Bahnschrift SemiBold', align='Right', fmt='h:m:s')]
    modules = [('CPU TEMP', 'CPUTEMP', 'temp', 'DEG C'), ('CPU LOAD', 'CPULOAD', 'cpu', 'PERCENT'), ('FRAMERATE', 'FPS', 'game', 'FPS'),
               ('GPU TEMP', 'GPUTEMP', 'temp', 'DEG C'), ('GPU LOAD', 'GPULOAD', 'gpu', 'PERCENT')]
    w, gap, x0 = 362, 15, 24
    for i, (title, data, icon_name, unit) in enumerate(modules):
        x = x0 + i * (w + gap)
        e += [panel(x, 70, w, 386, **module),
              icon(x + 26, 96, icon_name, 18, orange), text(x + 60, 94, title, 15, orange, 'Bahnschrift SemiBold'),
              line(x + 24, 132, x + w - 24, 132, '#66FF9A3C', 1.5),
              value(x + w / 2, 160, data, 70, cream, 'Bahnschrift SemiBold', align='Center', unit=False, glow=glow, glow_size=16),
              text(x + w / 2, 282, unit, 13, dim, 'Bahnschrift', align='Center'),
              bar(x + 26, 340, w - 52, 20, data, orange, '#33FF9A3C', segment=12, gap=4,
                  maximum=240 if data == 'FPS' else 100, glow=glow),
              line(x + 26, 392, x + 90, 392, orange, 3), text(x + w - 26, 384, f'0{i + 1}', 12, dim, 'Bahnschrift', align='Right')]
    return theme('Ember Ops', '#FF080706', e)


# --- Gauges -----------------------------------------------------------------------------------

def twin_gauges():
    white, dim, label = '#FFF2F6FA', '#FFAAB8C8', '#FF8C9BB0'
    e = [panel(0, 0, 1920, 480, '#FF172030', '#FF05070B', style='Radial', radius=0)]
    for cx, name, load, temp, start, end in [(300, 'CPU', 'CPULOAD', 'CPUTEMP', '#FF2EE6A8', '#FFF7D154'),
                                             (1620, 'GPU', 'GPULOAD', 'GPUTEMP', '#FF5AA9FF', '#FFE45CFF')]:
        e += [gauge(cx, 248, 340, 28, load, start, '#FF1D2533', gradient=end, glow='#55' + start[3:]),
              gauge(cx, 248, 270, 8, temp, '#FFFF8A65', '#FF1D2533', start=135, sweep=270, maximum=100),
              text(cx, 134, name, 18, label, 'Segoe UI', True, 'Center'),
              value(cx, 168, load, 60, white, 'Segoe UI Semibold', align='Center'),
              icon(cx - 52, 268, 'temp', 16, '#FFFF8A65'),
              value(cx + 12, 262, temp, 24, dim, 'Segoe UI Semibold', align='Center')]
    e += [value(960, 40, 'TIME', 96, white, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(960, 186, 'DATE', 20, dim, 'Segoe UI', align='Center')]
    tiles = [(620, 'FPS', 'FPS', 'game', False), (850, 'MEMORY', 'RAMLOAD', 'pulse', True), (1080, 'POWER', 'GPUPWR', 'power', True)]
    for x, title, data, icon_name, unit in tiles:
        e += [panel(x, 272, 220, 168, '#CC151C28', border='#26FFFFFF', border_width=1.5, radius=20),
              icon(x + 22, 294, icon_name, 15, label), text(x + 54, 292, title, 13, label, 'Segoe UI', True),
              value(x + 110, 334, data, 40, white, 'Segoe UI Semibold', align='Center', unit=unit)]
    return theme('Twin Gauges', '#FF05070B', e)


def cockpit():
    white, dim, amber = '#FFF4F7FA', '#FF9DAAB9', '#FFFFB547'
    e = [panel(0, 0, 1920, 480, '#FF11161E', '#FF07090D', radius=0),
         panel(20, 20, 1180, 440, '#E60D1219', border='#22FFFFFF', border_width=1.5, radius=26)]
    gauges = [(170, 'CPU LOAD', 'CPULOAD', '#FF35E0B0'), (460, 'CPU TEMP', 'CPUTEMP', amber),
              (750, 'GPU LOAD', 'GPULOAD', '#FF5AB0FF'), (1040, 'GPU TEMP', 'GPUTEMP', amber)]
    for cx, title, data, color in gauges:
        e += [gauge(cx, 214, 230, 20, data, color, '#FF1C2430', segment=7, gap=3, round_caps=False),
              value(cx, 176, data, 40, white, 'Segoe UI Semibold', align='Center'),
              text(cx, 336, title, 14, dim, 'Segoe UI', True, 'Center')]
    e += [line(60, 400, 1160, 400, '#1FFFFFFF', 1),
          value(60, 414, 'CPUMODEL', 12, dim, 'Segoe UI', unit=False),
          value(1160, 414, 'GPUMODEL', 12, dim, 'Segoe UI', unit=False, align='Right')]
    x = 1216
    e += [panel(x, 20, 684, 440, '#E60D1219', border='#22FFFFFF', border_width=1.5, radius=26),
          value(x + 36, 34, 'TIME', 72, white, 'Segoe UI Light', fmt='h:m'),
          value(x + 648, 54, 'DAY', 18, dim, 'Segoe UI', True, 'Right'), value(x + 648, 88, 'DATE', 16, dim, 'Segoe UI', align='Right'),
          icon(x + 36, 168, 'game', 16, amber), text(x + 70, 166, 'FPS', 14, dim, 'Segoe UI', True),
          value(x + 648, 150, 'FPS', 40, white, 'Segoe UI Semibold', align='Right', unit=False),
          chart(x + 36, 232, 612, 110, 'GPULOAD', '#FF5AB0FF', '#335AB0FF', 2.5),
          text(x + 36, 352, 'GPU LOAD · 60 s', 12, dim, 'Segoe UI', True),
          text(x + 36, 396, 'MEMORY', 13, dim, 'Segoe UI', True),
          bar(x + 160, 401, 360, 12, 'RAMLOAD', '#FF35E0B0', '#26FFFFFF', radius=6),
          value(x + 648, 388, 'RAMLOAD', 22, white, 'Segoe UI Semibold', align='Right')]
    return theme('Cockpit', '#FF07090D', e)


# --- Minimal / mono ---------------------------------------------------------------------------

def mono():
    white, grey, accent = '#FFFFFFFF', '#FFA8A8A8', '#FFFF6A45'
    e = [panel(0, 0, 1920, 480, '#FF000000', radius=0)]
    columns = [('CPU', 'CPULOAD', 'CPUTEMP', True), ('GPU', 'GPULOAD', 'GPUTEMP', True), ('FPS', 'FPS', 'FPS_AVG', False), ('TIME', 'TIME', 'DATE', False)]
    for i, (title, big, small, unit) in enumerate(columns):
        cx = 240 + i * 480
        if i: e.append(line(i * 480, 70, i * 480, 410, '#33FFFFFF', 1))
        e += [text(cx, 58, title, 15, accent, 'Segoe UI Semibold', align='Center'),
              value(cx, 104, big, 104, white, 'Segoe UI Light', align='Center', unit=unit, fmt='h:m' if big == 'TIME' else None),
              value(cx, 318, small, 24, grey, 'Segoe UI', align='Center', unit=unit)]
    e.append(text(1200, 358, 'AVG', 12, grey, 'Segoe UI', True, 'Center'))
    return theme('Mono', '#FF000000', e)


def big_clock():
    white, grey, accent = '#FFF5F5F7', '#FF9A9AA2', '#FF8FB8FF'
    e = [panel(0, 0, 1920, 480, '#FF101015', '#FF000000', angle=0, radius=0),
         value(576, 26, 'DAY', 20, accent, 'Segoe UI', True, 'Right'), value(592, 26, 'DATE', 20, grey, 'Segoe UI'),
         value(590, 50, 'TIME', 168, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         line(1180, 60, 1180, 420, '#2EFFFFFF', 1)]
    e += now_playing(150, 378, 76, 820, accent, white, grey, title_size=20, artist_size=15)
    rows = [('cpu', 'CPU', 'CPULOAD', 'CPUTEMP'), ('gpu', 'GPU', 'GPULOAD', 'GPUTEMP'), ('game', 'FPS', 'FPS', None), ('pulse', 'RAM', 'RAMLOAD', 'RAM_GB')]
    for i, (icon_name, title, a, b) in enumerate(rows):
        y = 64 + i * 92
        if i: e.append(line(1240, y - 18, 1860, y - 18, '#1FFFFFFF', 1))
        e += [icon(1240, y + 8, icon_name, 18, accent), text(1284, y + 6, title, 15, grey, 'Segoe UI', True),
              value(1600, y - 4, a, 34, white, 'Consolas', True, 'Right', unit=title != 'FPS')]
        if b: e.append(value(1860, y + 4, b, 22, grey, 'Consolas', align='Right'))
    return theme('Big Clock', '#FF000000', e)


# --- Music ------------------------------------------------------------------------------------

def vinyl():
    """Now playing first: a large cover with a record sliding out of its sleeve."""
    amber, cream, soft, label = '#FFFFB45C', '#FFFFF4E6', '#FFD8C2A8', '#FFB09478'
    e = [panel(0, 0, 1920, 480, '#FF22170F', '#FF070504', style='Radial', radius=0),
         # The record, drawn first so the cover sits on top of it.
         circle(590, 240, 400, '#FF2A2420', '#FF0A0908', border='#22FFFFFF', border_width=1)]
    e += [circle(590, 240, d, '#00000000', border='#16FFFFFF', border_width=1) for d in (350, 300, 250, 200)]
    e += [circle(590, 240, 140, amber, '#FFB8662A'),
          circle(590, 240, 14, '#FF070504'),
          album_art(40, 40, 400, radius=14, fill='#FF2A1E15', note=label)]
    x, w = 840, 720
    e += [value(x, 64, 'MEDIA_STATE', 14, amber, 'Segoe UI', True, unit=False),
          value(x, 100, 'MEDIA_TITLE', 44, cream, 'Segoe UI Semibold', unit=False, width=w),
          value(x, 186, 'MEDIA_ARTIST', 26, soft, 'Segoe UI', unit=False, width=w),
          line(x, 268, x + w, 268, '#33FFB45C')]
    for i, (icon_name, title, data, unit) in enumerate([('cpu', 'CPU', 'CPULOAD', True), ('gpu', 'GPU', 'GPULOAD', True),
                                                        ('game', 'FPS', 'FPS', False), ('pulse', 'RAM', 'RAMLOAD', True)]):
        mx = x + i * 184
        e += [icon(mx, 296, icon_name, 13, amber), text(mx + 28, 294, title, 12, label, 'Segoe UI', True),
              value(mx, 318, data, 28, cream, 'Segoe UI Semibold', unit=unit)]
    e += [chart(x, 392, w, 52, 'CPULOAD', '#99FFB45C', '#1FFFB45C', 2)]
    # Clock and temperatures on the right.
    x = 1600
    e += [panel(x, 24, 296, 432, '#B31E1712', border='#33FFB45C', border_width=1.5, radius=22),
          value(x + 148, 52, 'TIME', 52, cream, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(x + 148, 160, 'DAY', 15, amber, 'Segoe UI', True, 'Center'),
          value(x + 148, 192, 'DATE', 15, soft, 'Segoe UI', align='Center'),
          line(x + 28, 250, x + 268, 250, '#26FFFFFF'),
          icon(x + 28, 290, 'temp', 15, amber), text(x + 60, 288, 'CPU', 13, label, 'Segoe UI', True),
          value(x + 268, 280, 'CPUTEMP', 22, cream, 'Segoe UI Semibold', align='Right'),
          icon(x + 28, 370, 'temp', 15, amber), text(x + 60, 368, 'GPU', 13, label, 'Segoe UI', True),
          value(x + 268, 360, 'GPUTEMP', 22, cream, 'Segoe UI Semibold', align='Right')]
    return theme('Vinyl', '#FF070504', e)


def spotlight():
    """Now playing centre stage under a soft glow, with the clock and gauges either side."""
    violet, white, soft, label = '#FFC08CFF', '#FFFFFFFF', '#FFD2C6E6', '#FFA594C2'
    e = [panel(0, 0, 1920, 480, '#FF140D22', '#FF040306', style='Radial', radius=0),
         panel(560, -220, 800, 760, '#55B04AFF', '#00B04AFF', style='Radial', radius=0),
         value(960, 12, 'MEDIA_STATE', 11, violet, 'Segoe UI', True, 'Center', unit=False),
         album_art(810, 40, 300, radius=18, fill='#FF241A38', note=label),
         value(960, 356, 'MEDIA_TITLE', 26, white, 'Segoe UI Semibold', align='Center', unit=False, width=900),
         value(960, 408, 'MEDIA_ARTIST', 17, soft, 'Segoe UI', align='Center', unit=False, width=900)]
    # Left: the time.
    e += [value(340, 56, 'TIME', 92, white, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(330, 222, 'DAY', 17, violet, 'Segoe UI', True, 'Right'), value(350, 222, 'DATE', 17, soft, 'Segoe UI'),
          line(80, 290, 600, 290, '#26FFFFFF')]
    for cx, icon_name, title, data, unit in [(200, 'game', 'FPS', 'FPS', False), (480, 'pulse', 'RAM', 'RAMLOAD', True)]:
        e += [icon(cx - 34, 322, icon_name, 13, violet), text(cx - 6, 320, title, 12, label, 'Segoe UI', True),
              value(cx, 348, data, 32, white, 'Segoe UI Semibold', align='Center', unit=unit)]
    # Right: CPU and GPU gauges.
    for cx, name, load, temp, start, end in [(1430, 'CPU', 'CPULOAD', 'CPUTEMP', '#FF8C7CFF', '#FFFF7AC8'),
                                             (1720, 'GPU', 'GPULOAD', 'GPUTEMP', '#FF5AB8FF', '#FFC08CFF')]:
        cy = 214
        e += [gauge(cx, cy, 240, 18, load, start, '#FF221A33', gradient=end, glow='#55' + start[3:]),
              text(cx, cy - 74, name, 14, label, 'Segoe UI', True, 'Center'),
              value(cx, cy - 48, load, 38, white, 'Segoe UI Semibold', align='Center'),
              value(cx, cy + 54, temp, 18, soft, 'Segoe UI Semibold', align='Center')]
    return theme('Spotlight', '#FF040306', e)


# --- Retro / console --------------------------------------------------------------------------

def _lerp(a, b, t):
    """Blend two #AARRGGBB colors."""
    ca = [int(a[i:i + 2], 16) for i in range(1, 9, 2)]
    cb = [int(b[i:i + 2], 16) for i in range(1, 9, 2)]
    return '#' + ''.join(f'{round(x + (y - x) * t):02X}' for x, y in zip(ca, cb))


def synthwave():
    sky_top, sky_low = '#FF140A33', '#FF6B1A6E'
    white, cyan, pink, soft = '#FFFFFFFF', '#FF3FF2FF', '#FFFF4FD8', '#FFFFC2EE'
    horizon = 330
    e = [panel(0, 0, 1920, horizon, sky_top, sky_low, angle=90, radius=0)]
    e += [circle(x, y, d, '#B3FFFFFF') for x, y, d in [(140, 40, 3), (420, 120, 2), (660, 30, 3), (1290, 60, 2), (1520, 26, 3),
                                                       (1760, 130, 2), (300, 210, 2), (1650, 230, 2), (1110, 110, 2), (820, 70, 2)]]
    # The sun, cut by bands of sky towards the horizon.
    e += [panel(780, 150, 360, 360, '#FFFFE35A', '#FFFF3D8B', angle=90, radius=180, glow='#88FF3D8B', glow_size=40)]
    e += [panel(780, y, 360, h, _lerp(sky_top, sky_low, (y + h / 2) / horizon), radius=0) for y, h in [(258, 5), (276, 7), (294, 9), (312, 11)]]
    # Floor grid.
    e += [panel(0, horizon, 1920, 480 - horizon, '#FF1A0630', '#FF07020F', angle=90, radius=0)]
    e += [line(0, y, 1920, y, '#88FF4FD8', 1.5) for y in (340, 354, 374, 402, 438)]
    e += [line(960, horizon, 960 + i * 300, 480, '#77FF4FD8', 1.5) for i in range(-8, 9)]
    e += [line(0, horizon, 1920, horizon, pink, 2.5, glow=pink)]
    # Readouts.
    for x, align, name, load, temp in [(80, 'Left', 'CPU', 'CPULOAD', 'CPUTEMP'), (1840, 'Right', 'GPU', 'GPULOAD', 'GPUTEMP')]:
        e += [text(x, 52, name, 18, cyan, 'Bahnschrift Bold', align=align, glow='#663FF2FF'),
              value(x, 82, load, 72, white, 'Bahnschrift Bold', align=align, glow='#AAFF4FD8', glow_size=22),
              value(x, 214, temp, 24, soft, 'Bahnschrift SemiBold', align=align)]
    e += [value(960, 22, 'TIME', 54, white, 'Bahnschrift Bold', align='Center', fmt='h:m', glow='#AAFF4FD8', glow_size=20)]
    for x, icon_name, title, data, unit in [(80, 'game', 'FPS', 'FPS', False), (1320, 'pulse', 'MEMORY', 'RAMLOAD', True)]:
        e += [panel(x, 370, 520, 86, '#E0140626', border='#AA3FF2FF', border_width=1.5, radius=14, glow='#553FF2FF'),
              icon(x + 28, 400, icon_name, 15, cyan), text(x + 62, 398, title, 14, soft, 'Bahnschrift SemiBold'),
              value(x + 492, 384, data, 30, white, 'Bahnschrift Bold', align='Right', unit=unit)]
    return theme('Synthwave', '#FF07020F', e)


def terminal():
    green, dim, faint, bright = '#FF39FF72', '#FF27B850', '#FF1A7A36', '#FFB8FFCB'
    glow = '#6639FF72'
    mono = 'Consolas'
    e = [panel(0, 0, 1920, 480, '#FF07110A', '#FF020503', style='Radial', radius=0)]
    e += [line(0, y, 1920, y, '#0CFFFFFF', 1) for y in range(3, 480, 6)]  # scanlines
    e += [text(40, 18, 'pulsewave@rig:~$ watch -n1 sensors', 15, dim, mono),
          value(1880, 16, 'TIME', 16, green, mono, align='Right', fmt='h:m:s'),
          line(40, 52, 1880, 52, faint, 1, 6, 4)]
    rows = [('cpu.load', 'CPULOAD', 100, 'cpu.temp', 'CPUTEMP'), ('gpu.load', 'GPULOAD', 100, 'gpu.temp', 'GPUTEMP'),
            ('mem.load', 'RAMLOAD', 100, 'mem.used', 'RAM_GB'), ('vram.load', 'GPURAMLOAD', 100, 'gpu.pwr', 'GPUPWR'),
            ('fps', 'FPS', 240, 'fps.avg', 'FPS_AVG')]
    for i, (label, data, maximum, label2, data2) in enumerate(rows):
        y = 74 + i * 76
        e += [text(40, y + 10, label, 16, dim, mono),
              value(470, y, data, 28, bright, mono, True, 'Right', unit=data != 'FPS', glow=glow),
              text(500, y + 6, '[', 22, dim, mono), text(1084, y + 6, ']', 22, dim, mono),
              bar(522, y + 12, 556, 22, data, green, '#1439FF72', segment=10, gap=4, maximum=maximum, glow=glow),
              text(1112, y + 10, label2, 14, dim, mono),
              value(1420, y + 4, data2, 22, green, mono, align='Right', unit=data2 != 'FPS_AVG')]
    x = 1460
    e += [panel(x, 70, 420, 386, '#00000000', border=faint, border_width=1.5, radius=0),
          text(x + 20, 82, '[ host ]', 14, green, mono, True),
          value(x + 20, 118, 'CPUMODEL', 13, dim, mono, unit=False, width=380),
          value(x + 20, 146, 'GPUMODEL', 13, dim, mono, unit=False, width=380),
          line(x + 20, 186, x + 400, 186, faint, 1, 4, 4),
          value(x + 20, 196, 'TIME', 48, bright, mono, True, fmt='h:m', glow=glow, glow_size=16),
          value(x + 20, 290, 'DAY', 14, green, mono, True), value(x + 76, 290, 'DATE', 14, dim, mono),
          line(x + 20, 330, x + 400, 330, faint, 1, 4, 4),
          text(x + 20, 350, 'net.down', 14, dim, mono), value(x + 400, 346, 'DOWNDSPEED', 18, green, mono, align='Right'),
          text(x + 20, 396, 'net.up', 14, dim, mono), value(x + 400, 392, 'UPSPEED', 18, green, mono, align='Right')]
    return theme('Terminal', '#FF020503', e)


# --- Light -------------------------------------------------------------------------------------

def daylight():
    ink, grey, faint = '#FF1F2328', '#FF59616B', '#FFE6E2DA'
    blue, orange, green = '#FF2563EB', '#FFC2410C', '#FF15803D'
    card = dict(fill='#FFFFFFFF', gradient='#FFFBFAF7', border='#1F000000', border_width=1.5, radius=24, glow='#14000000', glow_size=18)
    e = [panel(0, 0, 1920, 480, '#FFF4F1EC', '#FFE6E0D6', angle=90, radius=0)]
    # Time and now playing.
    e += [panel(24, 24, 460, 432, **card),
          value(254, 58, 'TIME', 80, ink, 'Segoe UI Semibold', align='Center', fmt='h:m'),
          value(240, 214, 'DAY', 18, orange, 'Segoe UI', True, 'Right'), value(256, 214, 'DATE', 18, grey, 'Segoe UI'),
          line(52, 286, 456, 286, '#1A000000'),
          album_art(52, 318, 104, radius=14, fill='#FFEDE8E0', note='#FF8A8F98'),
          value(176, 322, 'MEDIA_STATE', 10, orange, 'Segoe UI', True, unit=False),
          value(176, 348, 'MEDIA_TITLE', 16, ink, 'Segoe UI Semibold', unit=False, width=280),
          value(176, 386, 'MEDIA_ARTIST', 13, grey, 'Segoe UI', unit=False, width=280)]
    # CPU and GPU.
    for x, name, icon_name, load, temp, power, clock, accent in [
            (500, 'PROCESSOR', 'cpu', 'CPULOAD', 'CPUTEMP', 'CPUPWR', 'CPUCLOCK_G', blue),
            (1016, 'GRAPHICS', 'gpu', 'GPULOAD', 'GPUTEMP', 'GPUPWR', 'GPUCLOCK_G', orange)]:
        cx = x + 150
        e += [panel(x, 24, 500, 432, **card),
              icon(x + 30, 48, icon_name, 15, accent), text(x + 62, 46, name, 13, grey, 'Segoe UI', True),
              gauge(cx, 250, 210, 18, load, accent, faint),
              value(cx, 216, load, 38, ink, 'Segoe UI Semibold', align='Center'),
              text(cx, 382, 'LOAD', 12, grey, 'Segoe UI', True, 'Center')]
        for k, (title, data) in enumerate([('TEMPERATURE', temp), ('POWER', power), ('CLOCK', clock)]):
            y = 120 + k * 100
            e += [text(x + 300, y, title, 11, grey, 'Segoe UI', True), value(x + 300, y + 20, data, 26, ink, 'Segoe UI Semibold')]
    # System.
    x = 1532
    e += [panel(x, 24, 364, 432, **card),
          icon(x + 30, 48, 'pulse', 15, green), text(x + 62, 46, 'MEMORY', 13, grey, 'Segoe UI', True),
          value(x + 334, 34, 'RAMLOAD', 28, ink, 'Segoe UI Semibold', align='Right'),
          bar(x + 30, 100, 304, 12, 'RAMLOAD', green, faint, radius=6),
          value(x + 30, 122, 'RAM_GB', 15, grey, 'Segoe UI'),
          line(x + 30, 172, x + 334, 172, '#1A000000'),
          icon(x + 30, 202, 'down', 16, blue), text(x + 62, 200, 'DOWN', 13, grey, 'Segoe UI', True),
          value(x + 334, 192, 'DOWNDSPEED', 22, ink, 'Segoe UI Semibold', align='Right'),
          icon(x + 30, 262, 'up', 16, orange), text(x + 62, 260, 'UP', 13, grey, 'Segoe UI', True),
          value(x + 334, 252, 'UPSPEED', 22, ink, 'Segoe UI Semibold', align='Right'),
          line(x + 30, 322, x + 334, 322, '#1A000000'),
          icon(x + 30, 362, 'game', 16, green), text(x + 62, 360, 'FPS', 13, grey, 'Segoe UI', True),
          value(x + 334, 346, 'FPS', 32, ink, 'Segoe UI Semibold', align='Right', unit=False)]
    return theme('Daylight', '#FFF4F1EC', e)


# --- Racing ------------------------------------------------------------------------------------

def redline():
    white, grey, red, amber = '#FFF5F5F5', '#FF9A9AA6', '#FFFF2D2D', '#FFFFB020'
    e = [panel(0, 0, 1920, 480, '#FF1D1D24', '#FF060608', style='Radial', radius=0)]
    e += [line(x, 0, x - 480, 480, '#08FFFFFF', 18) for x in range(0, 2400, 36)]  # carbon weave
    # Shift lights follow GPU load.
    e += [bar(560, 18, 800, 22, 'GPULOAD', red, '#FF1E1E24', segment=30, gap=8, glow='#88FF2D2D'),
          gauge(960, 270, 380, 26, 'FPS', white, '#FF202028', start=150, sweep=240, round_caps=False, gradient=red,
                segment=6, gap=3, maximum=240, glow='#66FF2D2D'),
          gauge(960, 270, 318, 3, 'FPS_AVG', '#88FFFFFF', '#00000000', start=150, sweep=240, round_caps=False, maximum=240),
          value(960, 196, 'FPS', 84, white, 'Bahnschrift Bold', align='Center', unit=False),
          text(960, 352, 'FPS', 16, red, 'Bahnschrift Bold', align='Center'),
          text(940, 392, 'AVG', 12, grey, 'Bahnschrift SemiBold', align='Right'),
          value(952, 388, 'FPS_AVG', 16, white, 'Bahnschrift SemiBold', unit=False)]
    for x, align, rows in [(56, 'Left', [('CPU TEMP', 'CPUTEMP'), ('GPU TEMP', 'GPUTEMP')]),
                           (1864, 'Right', [('TIME', 'TIME'), ('GPU POWER', 'GPUPWR')])]:
        for k, (title, data) in enumerate(rows):
            y = 70 + k * 170
            e += [text(x, y, title, 14, grey, 'Bahnschrift SemiBold', align=align),
                  value(x, y + 22, data, 48, white, 'Bahnschrift Bold', align=align, fmt='h:m:s' if data == 'TIME' else None)]
            if data.endswith('TEMP'):
                e.append(bar(x, y + 110, 480, 12, data, amber, '#FF202028', gradient=red, segment=12, gap=3))
    e += [text(56, 410, 'CPU LOAD', 13, grey, 'Bahnschrift SemiBold'), value(536, 402, 'CPULOAD', 22, white, 'Bahnschrift Bold', align='Right'),
          text(1384, 410, 'GPU LOAD', 13, grey, 'Bahnschrift SemiBold'), value(1864, 402, 'GPULOAD', 22, white, 'Bahnschrift Bold', align='Right')]
    return theme('Redline', '#FF060608', e)


# --- Dense grid --------------------------------------------------------------------------------

def tiles():
    bg, tile, text_color, label = '#FF2E3440', '#FF3B4252', '#FFECEFF4', '#FFD8DEE9'
    frost, blue, green, yellow, orange, red, purple = '#FF88C0D0', '#FF81A1C1', '#FFA3BE8C', '#FFEBCB8B', '#FFD08770', '#FFE88A93', '#FFB48EAD'
    w, h, gap, m = 300, 212, 16, 20
    e = [panel(0, 0, 1920, 480, bg, '#FF262B35', angle=90, radius=0)]
    cells = [  # row, column, icon, label, data, colour, bar maximum (None = no bar)
        (0, 0, 'cpu', 'CPU LOAD', 'CPULOAD', frost, 100), (0, 1, 'temp', 'CPU TEMP', 'CPUTEMP', orange, 100),
        (0, 2, 'power', 'CPU POWER', 'CPUPWR', yellow, 300), (0, 3, 'pulse', 'CPU CLOCK', 'CPUCLOCK_G', blue, 6),
        (1, 0, 'gpu', 'GPU LOAD', 'GPULOAD', green, 100), (1, 1, 'temp', 'GPU TEMP', 'GPUTEMP', red, 100),
        (1, 2, 'power', 'GPU POWER', 'GPUPWR', yellow, 450), (1, 3, 'game', 'FPS', 'FPS', purple, 240),
        (1, 4, 'pulse', 'MEMORY', 'RAMLOAD', frost, 100), (1, 5, 'down', 'DOWNLOAD', 'DOWNDSPEED', blue, None)]
    for row, col, icon_name, title, data, colour, maximum in cells:
        x, y = m + col * (w + gap), m + row * (h + gap)
        e += [panel(x, y, w, h, tile, border='#14FFFFFF', border_width=1, radius=18),
              icon(x + 24, y + 24, icon_name, 14, colour), text(x + 54, y + 22, title, 12, label, 'Segoe UI', True),
              value(x + 24, y + 62, data, 40, text_color, 'Segoe UI Semibold', unit=data != 'FPS')]
        if maximum: e.append(bar(x + 24, y + 172, w - 48, 10, data, colour, '#FF2E3440', radius=5, maximum=maximum))
        else: e += [icon(x + 24, y + 166, 'up', 14, colour), text(x + 54, y + 164, 'UP', 12, label, 'Segoe UI', True),
                    value(x + w - 24, y + 158, 'UPSPEED', 18, text_color, 'Segoe UI Semibold', align='Right')]
    x, y = m + 4 * (w + gap), m
    e += [panel(x, y, 2 * w + gap, h, tile, border='#14FFFFFF', border_width=1, radius=18),
          value(x + w + gap / 2, y + 36, 'TIME', 72, text_color, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(x + w + gap / 2 - 8, y + 168, 'DAY', 16, frost, 'Segoe UI', True, 'Right'),
          value(x + w + gap / 2 + 8, y + 168, 'DATE', 16, label, 'Segoe UI')]
    return theme('Tiles', bg, e)


# --- Other screens --------------------------------------------------------------------------

def _readout_card(x, y, w, h, kind, accent, white, dim, label, card, gauge_d, gauge_t, value_pt, extra, extra_label, chart_h=0):
    """A CPU or GPU card for the larger screens: load gauge with the value inside, temperature and a second reading below."""
    model, load, temp = ('CPUMODEL', 'CPULOAD', 'CPUTEMP') if kind == 'CPU' else ('GPUMODEL', 'GPULOAD', 'GPUTEMP')
    cx, cy = x + w / 2, y + 64 + gauge_d / 2 + gauge_t
    vpx = value_pt * 4 / 3
    e = [panel(x, y, w, h, **card),
         icon(x + 32, y + 30, 'cpu' if kind == 'CPU' else 'gpu', 18, accent), text(x + 72, y + 28, kind, 16, accent, 'Segoe UI', True),
         value(x + 140, y + 33, model, 13, dim, 'Segoe UI', unit=False, width=w - 172),
         gauge(cx, cy, gauge_d, gauge_t, load, accent, '#26FFFFFF', glow=accent.replace('#FF', '#66')),
         value(cx, cy - vpx * 0.72, load, value_pt, white, 'Segoe UI Semibold', align='Center'),
         text(cx, cy + vpx * 0.62, 'LOAD', 13, label, 'Segoe UI', True, 'Center')]
    ry = cy + gauge_d / 2 + gauge_t + 22
    e += [text(x + 32, ry, 'TEMP', 13, label, 'Segoe UI', True),
          value(x + 32, ry + 24, temp, 30, accent, 'Segoe UI Semibold'),
          text(x + w - 32, ry, extra_label, 13, label, 'Segoe UI', True, 'Right'),
          value(x + w - 32, ry + 24, extra, 30, white, 'Segoe UI Semibold', align='Right', unit=extra != 'FPS')]
    if chart_h:
        e.append(chart(x + 32, y + h - chart_h - 24, w - 64, chart_h, load, accent, accent.replace('#FF', '#2E'), 2.5))
    return e


def infinity():
    """HYTE Y70 Touch Infinite, turned landscape (2560x682): media with touch buttons, CPU, GPU and system cards."""
    cyan, magenta, violet = '#FF5CE1E6', '#FFFF7AC6', '#FFB79CFF'
    white, dim, label = '#FFF5F7FF', '#FFC9D1EC', '#FFA9B4DA'
    card = dict(fill='#E6141A33', gradient='#E60C1024', border='#33FFFFFF', border_width=1.5, radius=28)
    e = [panel(0, 0, 2560, 682, '#FF1A1F45', '#FF04050C', style='Radial', radius=0),
         panel(24, 24, 880, 634, **card),
         album_art(60, 60, 360, radius=20, fill='#FF1C2140', note=label)]
    tx, tw = 456, 420
    e += [value(tx, 70, 'MEDIA_STATE', 14, magenta, 'Segoe UI', True, unit=False),
          value(tx, 108, 'MEDIA_TITLE', 32, white, 'Segoe UI Semibold', unit=False, width=tw),
          value(tx, 188, 'MEDIA_ARTIST', 21, dim, 'Segoe UI', unit=False, width=tw)]
    e += media_buttons(tx + tw / 2, 340, 104, 30, white, '#FF262C55', '#55FF7AC6')
    e += [value(60, 452, 'TIME', 72, white, 'Segoe UI Light', fmt='h:m'),
          value(470, 488, 'DAY', 20, magenta, 'Segoe UI', True),
          value(470, 528, 'DATE', 20, dim, 'Segoe UI')]
    e += _readout_card(928, 24, 520, 634, 'CPU', cyan, white, dim, label, card, 290, 24, 52, 'CPUPWR', 'POWER', chart_h=56)
    e += _readout_card(1472, 24, 520, 634, 'GPU', violet, white, dim, label, card, 290, 24, 52, 'FPS', 'FPS', chart_h=56)
    x, w = 2016, 520
    rx = x + w - 36
    e += [panel(x, 24, w, 634, **card),
          icon(x + 32, 54, 'pulse', 18, magenta), text(x + 72, 52, 'SYSTEM', 16, magenta, 'Segoe UI', True),
          text(x + 36, 132, 'MEMORY', 13, label, 'Segoe UI', True),
          value(rx, 104, 'RAMLOAD', 44, white, 'Segoe UI Semibold', align='Right'),
          bar(x + 36, 188, w - 72, 14, 'RAMLOAD', magenta, '#26FFFFFF', radius=7),
          value(x + 36, 214, 'RAM_GB', 18, dim, 'Segoe UI'), text(rx, 216, 'IN USE', 12, label, 'Segoe UI', True, 'Right'),
          line(x + 36, 276, rx, 276, '#26FFFFFF')]
    for i, (name, title, data, colour) in enumerate([('down', 'DOWNLOAD', 'DOWNDSPEED', cyan), ('up', 'UPLOAD', 'UPSPEED', violet),
                                                       ('pulse', 'CPU FAN', 'CPUFAN', cyan), ('gpu', 'VIDEO MEMORY', 'GPURAM', violet)]):
        y = 306 + i * 86
        e += [icon(x + 36, y + 10, name, 18, colour), text(x + 74, y + 12, title, 13, label, 'Segoe UI', True),
              value(rx, y, data, 26, white, 'Segoe UI Semibold', align='Right')]
    return theme('Infinity', '#FF04050C', e, 2560, 682)


def tower():
    """Vertical panels (HYTE Y70 Touch Infinite upright at 682x2560, or a 480x1920 bar on its side)."""
    mint, sky, rose = '#FF6FF2D2', '#FF8CC4FF', '#FFFF9DB8'
    white, dim, label = '#FFF4F8FC', '#FFC6D3E0', '#FFA3B6C9'
    card = dict(fill='#EB131C29', gradient='#EB0B111A', border='#2EFFFFFF', border_width=1.5, radius=30)
    cx = 341
    e = [panel(0, 0, 682, 2560, '#FF142235', '#FF05080D', angle=90, radius=0),
         panel(28, 28, 626, 360, **card),
         value(cx, 64, 'TIME', 96, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(cx - 10, 262, 'DAY', 22, mint, 'Segoe UI', True, 'Right'),
         value(cx + 10, 262, 'DATE', 22, dim, 'Segoe UI'),
         panel(28, 412, 626, 800, **card),
         album_art(91, 446, 500, radius=22, fill='#FF1B2636', note=label),
         value(cx, 968, 'MEDIA_STATE', 14, rose, 'Segoe UI', True, 'Center', unit=False),
         value(cx, 1000, 'MEDIA_TITLE', 28, white, 'Segoe UI Semibold', align='Center', unit=False, width=570),
         value(cx, 1056, 'MEDIA_ARTIST', 19, dim, 'Segoe UI', align='Center', unit=False, width=570)]
    e += media_buttons(cx, 1146, 100, 40, white, '#FF22303F', '#55FF9DB8')
    e += _readout_card(28, 1236, 626, 560, 'CPU', mint, white, dim, label, card, 300, 26, 54, 'CPUPWR', 'POWER')
    e += _readout_card(28, 1820, 626, 560, 'GPU', sky, white, dim, label, card, 300, 26, 54, 'FPS', 'FPS')
    e += [panel(28, 2404, 626, 128, **card)]
    for (x, align, title, data, colour) in [(64, 'Left', 'MEMORY', 'RAMLOAD', rose), (cx, 'Center', 'DOWN', 'DOWNDSPEED', mint),
                                            (618, 'Right', 'UP', 'UPSPEED', sky)]:
        e += [text(x, 2426, title, 13, colour, 'Segoe UI', True, align),
              value(x, 2452, data, 24, white, 'Segoe UI Semibold', align=align)]
    return theme('Tower', '#FF05080D', e, 682, 2560)


def desk_tablet():
    """A tablet on the desk (16:10, also fits 16:9): big tiles, media buttons and a next-theme button for touch."""
    teal, blue, amber = '#FF4FD8C4', '#FF7FB2FF', '#FFFFC163'
    white, dim, label = '#FFF6F8FB', '#FFCBD5E1', '#FFA7B4C6'
    card = dict(fill='#FF1A2230', gradient='#FF141A25', border='#24FFFFFF', border_width=1.5, radius=36)
    e = [panel(0, 0, 1920, 1200, '#FF101620', '#FF080B11', angle=90, radius=0),
         panel(32, 32, 700, 528, **card),
         value(382, 76, 'TIME', 120, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(372, 318, 'DAY', 26, teal, 'Segoe UI', True, 'Right'),
         value(392, 318, 'DATE', 26, dim, 'Segoe UI'),
         line(72, 400, 692, 400, '#22FFFFFF'),
         icon(72, 432, 'temp', 16, teal), text(108, 430, 'CPU', 14, label, 'Segoe UI', True),
         value(72, 462, 'CPUTEMP', 36, white, 'Segoe UI Semibold'),
         icon(400, 432, 'temp', 16, blue), text(436, 430, 'GPU', 14, label, 'Segoe UI', True),
         value(400, 462, 'GPUTEMP', 36, white, 'Segoe UI Semibold'),
         panel(756, 32, 1132, 528, **card),
         album_art(796, 72, 300, radius=24, fill='#FF222C3C', note=label)]
    tx, tw = 1136, 712
    e += [value(tx, 80, 'MEDIA_STATE', 16, amber, 'Segoe UI', True, unit=False),
          value(tx, 120, 'MEDIA_TITLE', 40, white, 'Segoe UI Semibold', unit=False, width=tw),
          value(tx, 210, 'MEDIA_ARTIST', 26, dim, 'Segoe UI', unit=False, width=tw)]
    e += media_buttons(tx + tw / 2, 400, 150, 56, white, '#FF283346', '#66FFC163')
    e += _readout_card(32, 584, 600, 584, 'CPU', teal, white, dim, label, card, 320, 28, 60, 'CPUPWR', 'POWER')
    e += _readout_card(660, 584, 600, 584, 'GPU', blue, white, dim, label, card, 320, 28, 60, 'FPS', 'FPS')
    x, w = 1288, 600
    rx = x + w - 40
    e += [panel(x, 584, w, 584, **card),
          icon(x + 32, 614, 'pulse', 18, amber), text(x + 72, 612, 'SYSTEM', 16, amber, 'Segoe UI', True),
          text(x + 40, 700, 'MEMORY', 14, label, 'Segoe UI', True),
          value(rx, 666, 'RAMLOAD', 56, white, 'Segoe UI Semibold', align='Right'),
          bar(x + 40, 770, w - 80, 16, 'RAMLOAD', amber, '#26FFFFFF', radius=8),
          value(x + 40, 800, 'RAM_GB', 20, dim, 'Segoe UI'), text(rx, 802, 'IN USE', 13, label, 'Segoe UI', True, 'Right'),
          icon(x + 40, 878, 'down', 20, teal), text(x + 82, 880, 'DOWNLOAD', 14, label, 'Segoe UI', True),
          value(rx, 864, 'DOWNDSPEED', 30, white, 'Segoe UI Semibold', align='Right'),
          icon(x + 40, 958, 'up', 20, blue), text(x + 82, 960, 'UPLOAD', 14, label, 'Segoe UI', True),
          value(rx, 944, 'UPSPEED', 30, white, 'Segoe UI Semibold', align='Right'),
          tap(panel(x + 40, 1052, w - 80, 84, '#FF283346', border='#66FFC163', border_width=1.5, radius=42), 'NextTheme'),
          tap(text(x + w / 2, 1074, 'NEXT THEME  ›', 18, white, 'Segoe UI', True, 'Center'), 'NextTheme')]
    return theme('Desk Tablet', '#FF080B11', e, 1920, 1200)


# --- Shortcuts and AI usage -------------------------------------------------------------------

DRAWER_GLYPH = ''


def shortcut(x, y, size, slot, fill, gradient=None, border=None, border_width=0, radius=24, glyph='#FFD7E3EE'):
    """A tile showing the icon of the user's shortcut in a slot (tray -> Shortcuts); tapping it opens the shortcut."""
    return dict(Kind='Shortcut', X=x, Y=y, Width=size, Height=size, Slot=slot, FillColor=fill, GradientColor=gradient,
                BorderColor=border or '#00000000', BorderWidth=border_width, CornerRadius=radius, LineColor=glyph)


def app_name(cx, y, slot, size, color, width):
    """The name of the shortcut in a slot, centred under its tile; tapping it opens the shortcut too."""
    e = value(cx, y, 'APP_NAME', size, color, 'Segoe UI', True, 'Center', unit=False, width=width)
    e['Slot'] = slot
    return e


def app_grid(x, y, columns, rows, tile, step_x, step_y, name_pt, name_color, first_slot=1, **style):
    out = []
    for r in range(rows):
        for c in range(columns):
            slot = first_slot + r * columns + c
            tx, ty = x + c * step_x, y + r * step_y
            out += [shortcut(tx, ty, tile, slot, **style), app_name(tx + tile / 2, ty + tile + 10, slot, name_pt, name_color, step_x - 12)]
    return out


def apps_button(x, y, w, h, size, color, fill, border):
    """Opens the app drawer with every shortcut."""
    px = size * 4 / 3
    return [tap(panel(x, y, w, h, fill, border=border, border_width=1.5, radius=h / 2), 'AppDrawer'),
            # Icon fonts sit higher in their line than Segoe UI; lower the glyph to the text's middle.
            tap(text(x + 36, y + (h - px * 1.33) / 2 + px * 0.3, DRAWER_GLYPH, size, color, 'Segoe Fluent Icons'), 'AppDrawer'),
            tap(text(x + w / 2 + 18, y + (h - px * 1.33) / 2, 'ALL APPS', size, color, 'Segoe UI', True, 'Center'), 'AppDrawer')]


def usage_gauge(cx, cy, d, t, data, title, accent, white, label, track):
    """A plan limit as a ring: % used inside, the window's name under it and when it resets below the ring."""
    vpx = 40 * 4 / 3
    return [gauge(cx, cy, d, t, data, accent, track),
            value(cx, cy - vpx * 0.72, data, 40, white, 'Segoe UI Semibold', align='Center'),
            text(cx, cy + vpx * 0.62, title, 12, label, 'Segoe UI', True, 'Center'),
            text(cx, cy + d / 2 + t / 2 + 12, 'RESETS IN', 11, label, 'Segoe UI', True, 'Center'),
            value(cx, cy + d / 2 + t / 2 + 32, data + '_RESET', 18, white, 'Segoe UI Semibold', align='Center', unit=False)]


def launchpad():
    """8.8" bar: ten shortcut tiles for games and programs, clock and temperatures, system loads and an All apps button."""
    teal, blue, violet = '#FF5FE0C8', '#FF86B6FF', '#FFB9A2FF'
    white, dim, label = '#FFF4F7FB', '#FFC9D4E0', '#FFA6B3C4'
    card = dict(fill='#E6141B26', gradient='#E60E131C', border='#26FFFFFF', border_width=1.5, radius=24)
    e = [panel(0, 0, 1920, 480, '#FF0E1520', '#FF05070B', angle=0, radius=0),
         panel(24, 24, 400, 432, **card),
         value(224, 52, 'TIME', 80, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(214, 206, 'DAY', 20, teal, 'Segoe UI', True, 'Right'),
         value(234, 206, 'DATE', 20, dim, 'Segoe UI'),
         line(64, 272, 384, 272, '#26FFFFFF'),
         icon(64, 300, 'temp', 16, teal), text(98, 298, 'CPU', 13, label, 'Segoe UI', True),
         value(64, 330, 'CPUTEMP', 34, white, 'Segoe UI Semibold'),
         icon(244, 300, 'temp', 16, blue), text(278, 298, 'GPU', 13, label, 'Segoe UI', True),
         value(244, 330, 'GPUTEMP', 34, white, 'Segoe UI Semibold')]
    e += app_grid(456, 36, 5, 2, 136, 164, 216, 14, white, fill='#FF182230', gradient='#FF111823', border='#33FFFFFF', border_width=1.5, radius=30)
    x, w = 1280, 616
    rx = x + w - 36
    e += [panel(x, 24, w, 432, **card),
          icon(x + 36, 54, 'pulse', 16, teal), text(x + 70, 52, 'SYSTEM', 14, teal, 'Segoe UI', True)]
    for i, (title, data, colour) in enumerate([('CPU', 'CPULOAD', teal), ('GPU', 'GPULOAD', blue), ('MEMORY', 'RAMLOAD', violet)]):
        y = 92 + i * 76
        e += [text(x + 36, y + 12, title, 13, label, 'Segoe UI', True),
              value(rx, y, data, 28, white, 'Segoe UI Semibold', align='Right'),
              bar(x + 36, y + 48, w - 72, 10, data, colour, '#24FFFFFF', radius=5)]
    e += apps_button(x + 36, 334, w - 72, 90, 22, white, '#FF22324A', '#665FE0C8')
    return theme('Launchpad', '#FF05070B', e)


def token_meter():
    """8.8" bar: Claude Code and Codex plan usage (5-hour and weekly limits, with reset times), clock and loads."""
    clay, sky, mint = '#FFF0A274', '#FF8DB8FF', '#FF6FE3C6'
    white, dim, label = '#FFF6F4F1', '#FFD3CEC8', '#FFB3ACA4'
    card = dict(fill='#E61A1715', gradient='#E6110F0E', border='#26FFFFFF', border_width=1.5, radius=24)
    e = [panel(0, 0, 1920, 480, '#FF15120F', '#FF070606', angle=0, radius=0),
         panel(24, 24, 320, 432, **card),
         value(184, 56, 'TIME', 60, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(174, 172, 'DAY', 18, clay, 'Segoe UI', True, 'Right'),
         value(194, 172, 'DATE', 18, dim, 'Segoe UI'),
         line(60, 232, 308, 232, '#26FFFFFF')]
    for i, (title, data, colour) in enumerate([('CPU', 'CPULOAD', mint), ('GPU', 'GPULOAD', sky)]):
        y = 256 + i * 96
        e += [text(60, y + 10, title, 13, label, 'Segoe UI', True),
              value(308, y, data, 28, white, 'Segoe UI Semibold', align='Right'),
              bar(60, y + 50, 248, 10, data, colour, '#24FFFFFF', radius=5)]
    for x, w, name, key, accent in [(368, 760, 'CLAUDE CODE', 'CLAUDE', clay), (1152, 744, 'CODEX', 'CODEX', sky)]:
        e += [panel(x, 24, w, 432, **card),
              text(x + 36, 46, name, 18, accent, 'Segoe UI', True),
              text(x + w - 36, 52, 'PLAN LIMITS', 12, label, 'Segoe UI', True, 'Right')]
        e += usage_gauge(x + w * 0.27, 228, 196, 20, key + '_5H', '5-HOUR', accent, white, label, '#26FFFFFF')
        e += usage_gauge(x + w * 0.73, 228, 196, 20, key + '_WEEK', 'WEEKLY', accent, white, label, '#26FFFFFF')
    return theme('Token Meter', '#FF070606', e)


def command_deck():
    """A tablet on the desk (16:10): twelve big shortcut tiles with names, Claude Code and Codex usage, clock and loads."""
    teal, clay, sky = '#FF5FE0C8', '#FFF0A274', '#FF8DB8FF'
    white, dim, label = '#FFF5F8FB', '#FFCBD5E1', '#FFA7B4C6'
    card = dict(fill='#FF19212C', gradient='#FF131921', border='#24FFFFFF', border_width=1.5, radius=36)
    e = [panel(0, 0, 1920, 1200, '#FF101620', '#FF07090D', angle=90, radius=0),
         panel(32, 32, 560, 380, **card),
         value(312, 60, 'TIME', 104, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(302, 256, 'DAY', 24, teal, 'Segoe UI', True, 'Right'),
         value(322, 256, 'DATE', 24, dim, 'Segoe UI')]
    for i, (title, data, colour) in enumerate([('CPU', 'CPULOAD', teal), ('GPU', 'GPULOAD', sky)]):
        x = 72 + i * 260
        e += [text(x, 318, title, 14, label, 'Segoe UI', True),
              value(x + 220, 304, data, 30, white, 'Segoe UI Semibold', align='Right'),
              bar(x, 366, 220, 10, data, colour, '#24FFFFFF', radius=5)]
    x, w = 616, 1272
    e += [panel(x, 32, w, 380, **card),
          text(x + 40, 58, 'AI USAGE', 16, clay, 'Segoe UI', True),
          text(x + w - 40, 62, 'CLAUDE CODE  ·  CODEX', 13, label, 'Segoe UI', True, 'Right')]
    for i, (key, title, accent) in enumerate([('CLAUDE_5H', 'CLAUDE 5H', clay), ('CLAUDE_WEEK', 'CLAUDE WEEK', clay),
                                              ('CODEX_5H', 'CODEX 5H', sky), ('CODEX_WEEK', 'CODEX WEEK', sky)]):
        e += usage_gauge(x + 159 + i * 318, 222, 184, 18, key, title, accent, white, label, '#26FFFFFF')
    e += [panel(32, 436, 1856, 732, **card),
          icon(92, 470, 'game', 18, teal), text(130, 466, 'APPS & GAMES', 16, teal, 'Segoe UI', True)]
    e += apps_button(1528, 456, 320, 60, 16, white, '#FF22324A', '#665FE0C8')
    e += app_grid(140, 540, 6, 2, 200, 296, 300, 18, white, fill='#FF222C3A', gradient='#FF1A2230', border='#33FFFFFF', border_width=1.5, radius=44)
    return theme('Command Deck', '#FF07090D', e, 1920, 1200)


# --- Vertical (HYTE Y70 Touch Infinite upright, 682x2560) -----------------------------------------

def pillar():
    """Neon stack: CPU and GPU gauges with temperatures, memory, network charts and compact now playing."""
    cyan, magenta, violet = '#FF4FE3FF', '#FFFF5CC8', '#FFA98BFF'
    white, dim, label = '#FFF4F8FF', '#FFC9D2EA', '#FFA9B6D6'
    track = '#22FFFFFF'
    cx = 341
    e = [panel(0, 0, 682, 2560, '#FF141A33', '#FF04050B', style='Radial', radius=0),
         value(cx, 60, 'TIME', 88, white, 'Segoe UI Light', align='Center', fmt='h:m', glow='#664FE3FF', glow_size=18),
         value(cx - 10, 236, 'DAY', 22, magenta, 'Segoe UI', True, 'Right'),
         value(cx + 10, 236, 'DATE', 22, dim, 'Segoe UI'),
         line(60, 318, 622, 318, '#554FE3FF', 2, glow='#884FE3FF')]
    for top, title, model, load, temp, extra, extra_label, accent in [
            (352, 'PROCESSOR', 'CPUMODEL', 'CPULOAD', 'CPUTEMP', 'CPUPWR', 'POWER', cyan),
            (972, 'GRAPHICS', 'GPUMODEL', 'GPULOAD', 'GPUTEMP', 'FPS', 'FPS', magenta)]:
        cy = top + 268
        e += [text(cx, top, title, 16, accent, 'Segoe UI', True, 'Center'),
              value(cx, top + 30, model, 14, dim, 'Segoe UI', align='Center', unit=False, width=560),
              gauge(cx, cy, 400, 30, load, accent, track, glow=accent.replace('#FF', '#77')),
              value(cx, cy - 96 * 0.72, load, 72, white, 'Segoe UI Semibold', align='Center'),
              text(cx, cy + 96 * 0.62, 'LOAD', 16, label, 'Segoe UI', True, 'Center'),
              text(cx - 100, cy + 150, 'TEMP', 14, label, 'Segoe UI', True, 'Center'),
              value(cx - 100, cy + 174, temp, 34, accent, 'Segoe UI Semibold', align='Center'),
              text(cx + 100, cy + 150, extra_label, 14, label, 'Segoe UI', True, 'Center'),
              value(cx + 100, cy + 174, extra, 34, white, 'Segoe UI Semibold', align='Center', unit=extra != 'FPS')]
    e += [line(60, 1530, 622, 1530, '#33A98BFF', 1.5),
          icon(60, 1572, 'pulse', 16, violet), text(94, 1570, 'MEMORY', 16, violet, 'Segoe UI', True),
          value(622, 1552, 'RAMLOAD', 44, white, 'Segoe UI Semibold', align='Right'),
          bar(60, 1630, 562, 14, 'RAMLOAD', violet, track, radius=7, glow='#66A98BFF'),
          value(60, 1662, 'RAM_GB', 18, dim, 'Segoe UI'), text(622, 1664, 'IN USE', 13, label, 'Segoe UI', True, 'Right')]
    for y, name, title, data, accent in [(1748, 'down', 'DOWNLOAD', 'DOWNDSPEED', cyan), (1948, 'up', 'UPLOAD', 'UPSPEED', magenta)]:
        e += [icon(60, y + 12, name, 16, accent), text(94, y + 10, title, 14, accent, 'Segoe UI', True),
              value(622, y, data, 28, white, 'Segoe UI Semibold', align='Right'),
              chart(60, y + 56, 562, 120, data, accent, accent.replace('#FF', '#2E'), 2.5, maximum=0)]
    e += [line(60, 2156, 622, 2156, '#33FF5CC8', 1.5),
          album_art(60, 2186, 140, radius=18, fill='#FF1A2040', note=label),
          value(224, 2192, 'MEDIA_STATE', 12, magenta, 'Segoe UI', True, unit=False),
          value(224, 2222, 'MEDIA_TITLE', 20, white, 'Segoe UI Semibold', unit=False, width=398),
          value(224, 2266, 'MEDIA_ARTIST', 16, dim, 'Segoe UI', unit=False, width=398)]
    e += media_buttons(cx, 2430, 96, 44, white, '#FF1C2246', '#664FE3FF')
    return theme('Pillar', '#FF04050B', e, 682, 2560)


def spin():
    """Music first: big album art, title and touch media buttons, then clock and a system card."""
    coral, gold, teal = '#FFFF8A6B', '#FFFFC86B', '#FF5FE0C8'
    white, dim, label = '#FFFBF6F2', '#FFD9CEC6', '#FFB9ADA4'
    card = dict(fill='#E61D1517', gradient='#E6130E10', border='#26FFFFFF', border_width=1.5, radius=32)
    track = '#24FFFFFF'
    cx = 341
    e = [panel(0, 0, 682, 2560, '#FF2A1A22', '#FF0A0709', angle=90, radius=0),
         album_art(60, 80, 562, radius=32, fill='#FF2A1E22', note=label),
         value(cx, 680, 'MEDIA_STATE', 16, coral, 'Segoe UI', True, 'Center', unit=False),
         value(cx, 716, 'MEDIA_TITLE', 34, white, 'Segoe UI Semibold', align='Center', unit=False, width=600),
         value(cx, 790, 'MEDIA_ARTIST', 24, dim, 'Segoe UI', align='Center', unit=False, width=600)]
    e += media_buttons(cx, 950, 140, 40, white, '#FF2E2226', '#66FF8A6B')
    e += [panel(40, 1100, 602, 300, **card),
          value(cx, 1128, 'TIME', 80, white, 'Segoe UI Light', align='Center', fmt='h:m'),
          value(cx - 10, 1300, 'DAY', 22, gold, 'Segoe UI', True, 'Right'),
          value(cx + 10, 1300, 'DATE', 22, dim, 'Segoe UI'),
          panel(40, 1430, 602, 1090, **card),
          icon(80, 1466, 'pulse', 16, gold), text(114, 1464, 'SYSTEM', 16, gold, 'Segoe UI', True)]
    for x, data, title, temp, accent in [(191, 'CPULOAD', 'CPU', 'CPUTEMP', teal), (491, 'GPULOAD', 'GPU', 'GPUTEMP', coral)]:
        cy = 1660
        e += [gauge(x, cy, 220, 22, data, accent, track),
              value(x, cy - 59 * 0.72, data, 44, white, 'Segoe UI Semibold', align='Center'),
              text(x, cy + 59 * 0.62, title, 13, label, 'Segoe UI', True, 'Center'),
              text(x, 1810, title + ' TEMP', 13, label, 'Segoe UI', True, 'Center'),
              value(x, 1834, temp, 30, accent, 'Segoe UI Semibold', align='Center')]
    e += [line(80, 1920, 602, 1920, '#22FFFFFF'),
          text(80, 1972, 'FRAMES PER SECOND', 13, label, 'Segoe UI', True),
          value(602, 1944, 'FPS', 48, white, 'Segoe UI Semibold', align='Right', unit=False),
          text(80, 2066, 'MEMORY', 13, label, 'Segoe UI', True),
          value(602, 2050, 'RAMLOAD', 28, white, 'Segoe UI Semibold', align='Right'),
          bar(80, 2104, 522, 12, 'RAMLOAD', gold, track, radius=6),
          icon(80, 2164, 'down', 16, teal), text(112, 2162, 'DOWN', 13, label, 'Segoe UI', True),
          value(602, 2150, 'DOWNDSPEED', 26, white, 'Segoe UI Semibold', align='Right'),
          icon(80, 2224, 'up', 16, coral), text(112, 2222, 'UP', 13, label, 'Segoe UI', True),
          value(602, 2210, 'UPSPEED', 26, white, 'Segoe UI Semibold', align='Right'),
          chart(80, 2300, 522, 180, 'CPULOAD', teal, '#2E5FE0C8', 2.5)]
    return theme('Spin', '#FF0A0709', e, 682, 2560)


def command_column():
    """Touch launcher column: clock, twelve shortcut tiles with All apps, Claude Code and Codex usage, system loads."""
    teal, clay, sky = '#FF5FE0C8', '#FFF0A274', '#FF8DB8FF'
    white, dim, label = '#FFF5F8FB', '#FFCBD5E1', '#FFA7B4C6'
    card = dict(fill='#FF19212C', gradient='#FF131921', border='#24FFFFFF', border_width=1.5, radius=32)
    track = '#26FFFFFF'
    cx = 341
    e = [panel(0, 0, 682, 2560, '#FF101620', '#FF07090D', angle=90, radius=0),
         panel(28, 28, 626, 300, **card),
         value(cx, 52, 'TIME', 84, white, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(cx - 10, 236, 'DAY', 20, teal, 'Segoe UI', True, 'Right'),
         value(cx + 10, 236, 'DATE', 20, dim, 'Segoe UI'),
         panel(28, 356, 626, 1100, **card),
         icon(68, 392, 'game', 16, teal), text(102, 388, 'APPS & GAMES', 16, teal, 'Segoe UI', True)]
    e += app_grid(76, 450, 3, 4, 150, 190, 226, 15, white, fill='#FF222C3A', gradient='#FF1A2230', border='#33FFFFFF', border_width=1.5, radius=36)
    e += apps_button(68, 1366, 546, 66, 18, white, '#FF22324A', '#665FE0C8')
    e += [panel(28, 1480, 626, 684, **card),
          text(68, 1512, 'AI USAGE', 16, clay, 'Segoe UI', True),
          text(614, 1516, 'CLAUDE  ·  CODEX', 12, label, 'Segoe UI', True, 'Right')]
    for i, (key, title, accent) in enumerate([('CLAUDE_5H', 'CLAUDE 5H', clay), ('CLAUDE_WEEK', 'CLAUDE WEEK', clay),
                                              ('CODEX_5H', 'CODEX 5H', sky), ('CODEX_WEEK', 'CODEX WEEK', sky)]):
        e += usage_gauge(191 + (i % 2) * 300, 1680 + (i // 2) * 300, 170, 16, key, title, accent, white, label, track)
    e += [panel(28, 2192, 626, 340, **card)]
    for i, (title, data, accent) in enumerate([('CPU', 'CPULOAD', teal), ('GPU', 'GPULOAD', sky)]):
        y = 2222 + i * 76
        e += [text(68, y + 10, title, 14, label, 'Segoe UI', True),
              value(614, y, data, 28, white, 'Segoe UI Semibold', align='Right'),
              bar(68, y + 50, 546, 10, data, accent, track, radius=5)]
    e += [icon(68, 2400, 'temp', 16, teal), text(100, 2398, 'CPU', 13, label, 'Segoe UI', True),
          value(68, 2428, 'CPUTEMP', 32, white, 'Segoe UI Semibold'),
          icon(360, 2400, 'temp', 16, sky), text(392, 2398, 'GPU', 13, label, 'Segoe UI', True),
          value(360, 2428, 'GPUTEMP', 32, white, 'Segoe UI Semibold')]
    return theme('Command Column', '#FF07090D', e, 682, 2560)


def daylight_column():
    """Light theme, Task Manager style: a card per component with its main reading, details and a chart."""
    blue, green, orange = '#FF2563EB', '#FF0E8A5F', '#FFE0661B'
    ink, label = '#FF18202B', '#FF566273'
    card = dict(fill='#FFFFFFFF', border='#1A0B1A2E', border_width=1.5, radius=28)
    cx = 341
    e = [panel(0, 0, 682, 2560, '#FFF3F5F8', '#FFE7EBF1', angle=90, radius=0),
         panel(28, 28, 626, 300, **card),
         value(cx, 52, 'TIME', 84, ink, 'Segoe UI Light', align='Center', fmt='h:m'),
         value(cx - 10, 236, 'DAY', 20, blue, 'Segoe UI', True, 'Right'),
         value(cx + 10, 236, 'DATE', 20, label, 'Segoe UI')]

    def section(top, height, glyph, title, accent, model=None):
        out = [panel(28, top, 626, height, **card),
               icon(68, top + 34, glyph, 18, accent), text(104, top + 30, title, 16, ink, 'Segoe UI', True)]
        if model:
            out.append(value(614, top + 34, model, 12, label, 'Segoe UI', align='Right', unit=False, width=360))
        return out

    for top, glyph, title, model, load, accent, pairs, foot_label, foot in [
            (352, 'cpu', 'CPU', 'CPUMODEL', 'CPULOAD', blue, [('TEMP', 'CPUTEMP'), ('POWER', 'CPUPWR')], 'CLOCK', 'CPUCLOCK_G'),
            (876, 'gpu', 'GPU', 'GPUMODEL', 'GPULOAD', green, [('TEMP', 'GPUTEMP'), ('FPS', 'FPS')], 'VIDEO MEMORY', 'GPURAM')]:
        e += section(top, 500, glyph, title, accent, model)
        e += [value(68, top + 70, load, 60, ink, 'Segoe UI Semibold')]
        for i, (name, data) in enumerate(pairs):
            x = 380 + i * 140
            e += [text(x, top + 84, name, 12, label, 'Segoe UI', True),
                  value(x, top + 104, data, 26, ink, 'Segoe UI Semibold', unit=data != 'FPS')]
        e += [chart(68, top + 196, 546, 230, load, accent, accent.replace('#FF', '#26'), 2.5, border='#140B1A2E', border_width=1),
              text(68, top + 448, foot_label, 12, label, 'Segoe UI', True),
              value(614, top + 440, foot, 20, ink, 'Segoe UI Semibold', align='Right')]
    top = 1400
    e += section(top, 320, 'pulse', 'MEMORY', orange)
    e += [value(68, top + 70, 'RAMLOAD', 60, ink, 'Segoe UI Semibold'),
          text(380, top + 84, 'IN USE', 12, label, 'Segoe UI', True), value(380, top + 104, 'RAM_GB', 26, ink, 'Segoe UI Semibold'),
          text(614, top + 84, 'TOTAL', 12, label, 'Segoe UI', True, 'Right'), value(614, top + 104, 'RAMTOTAL_GB', 26, ink, 'Segoe UI Semibold', align='Right'),
          bar(68, top + 206, 546, 16, 'RAMLOAD', orange, '#1F0B1A2E', radius=8),
          text(68, top + 258, 'FREE', 12, label, 'Segoe UI', True),
          value(614, top + 250, 'RAMVALID_GB', 20, ink, 'Segoe UI Semibold', align='Right')]
    top = 1744
    e += section(top, 500, 'network', 'NETWORK', green)
    e += [text(68, top + 84, 'DOWNLOAD', 12, label, 'Segoe UI', True), value(68, top + 104, 'DOWNDSPEED', 28, ink, 'Segoe UI Semibold'),
          text(380, top + 84, 'UPLOAD', 12, label, 'Segoe UI', True), value(380, top + 104, 'UPSPEED', 28, ink, 'Segoe UI Semibold'),
          chart(68, top + 170, 546, 140, 'DOWNDSPEED', green, '#260E8A5F', 2.5, maximum=0, border='#140B1A2E', border_width=1),
          chart(68, top + 330, 546, 140, 'UPSPEED', orange, '#26E0661B', 2.5, maximum=0, border='#140B1A2E', border_width=1)]
    top = 2268
    e += [panel(28, top, 626, 264, **card),
          album_art(60, top + 32, 200, radius=18, fill='#FFE7EBF1', note=label),
          value(284, top + 40, 'MEDIA_STATE', 12, blue, 'Segoe UI', True, unit=False),
          value(284, top + 68, 'MEDIA_TITLE', 22, ink, 'Segoe UI Semibold', unit=False, width=330),
          value(284, top + 114, 'MEDIA_ARTIST', 16, label, 'Segoe UI', unit=False, width=330)]
    e += media_buttons(449, top + 200, 64, 24, ink, '#FFEFF2F6', '#330B1A2E')
    return theme('Daylight Column', '#FFE7EBF1', e, 682, 2560)


TEMPLATES = [('01-glass-cards', glass_cards), ('02-aurora', aurora), ('03-vector-hud', vector_hud), ('04-ember-ops', ember_ops),
             ('05-twin-gauges', twin_gauges), ('06-cockpit', cockpit), ('07-mono', mono), ('08-big-clock', big_clock),
             ('09-vinyl', vinyl), ('10-spotlight', spotlight), ('11-synthwave', synthwave), ('12-terminal', terminal),
             ('13-daylight', daylight), ('14-redline', redline), ('15-tiles', tiles),
             ('16-infinity', infinity), ('17-tower', tower), ('18-desk-tablet', desk_tablet),
             ('19-launchpad', launchpad), ('20-token-meter', token_meter), ('21-command-deck', command_deck),
             ('22-pillar', pillar), ('23-spin', spin), ('24-command-column', command_column), ('25-daylight-column', daylight_column)]

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name, build in TEMPLATES:
        with open(os.path.join(OUT, name + '.json'), 'w', encoding='utf-8', newline='\n') as f:
            json.dump(build(), f, indent=2, ensure_ascii=False)
            f.write('\n')
        print('wrote', name)
