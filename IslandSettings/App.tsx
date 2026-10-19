import React, {useCallback, useEffect, useState} from 'react';
import {ActivityIndicator, Pressable, ScrollView, StyleSheet, Switch, Text, TextInput, View} from 'react-native';

type Settings = {
  surfaceOpacity: number; edge: number; alignment: number;
  horizontalOffset: number; verticalOffset: number; theme: number;
  surfaceStyle: number; accentColor: string; backgroundColor: string; gradientStartColor: string;
  gradientEndColor: string; gradientDirection: number; logoStyle: number;
  useAlbumArtBackdrop: boolean; useMediaArtworkColors: boolean; useAudioReactiveEffects: boolean;
  minimizedAppearance: number;
  compactContent: number; hoverContent: number; expandedPanel: number;
  tempVaultRoot: string; tempVaultAutoCopy: boolean; tempVaultConnected: boolean;
  panelWidth: number; panelHeight: number; appPanelWidth: number; appPanelHeight: number;
  notificationsSupported: boolean;
  notificationsEnabled: boolean; notificationStatus: string; hasCustomMiniLogo: boolean;
  notificationBehavior: number;
  notificationRules: {match: string; behavior: number; accentColor: string}[];
};
type Section = 'Position' | 'Notch' | 'Appearance' | 'Alerts' | 'Storage';
const endpoint = 'http://127.0.0.1:38691/api/settings';
const headers = {'X-DynamicIsland-Settings': '1', 'Content-Type': 'application/json'};
const defaultAccent = '#B68CFF';
const sectionMeta: Record<Section, {glyph: string; summary: string}> = {
  Position: {glyph: '\uE707', summary: 'Placement and workspace dimensions'},
  Notch: {glyph: '\uF0E2', summary: 'Compact, hover and expanded content'},
  Appearance: {glyph: '\uE790', summary: 'Color, material and identity'},
  Alerts: {glyph: '\uEA8F', summary: 'Per-app and per-person notification rules'},
  Storage: {glyph: '\uE8B7', summary: 'Persistent files and TempVault'},
};

const darkUi = {shell: '#090A0E', sidebar: 'rgba(6, 7, 10, 0.94)', border: '#252731',
  card: 'rgba(20, 21, 27, 0.96)', cardBorder: '#2B2E38', control: '#0B0A0D',
  input: '#0B0A0D', primary: '#F1F4F2', secondary: '#909B9E', muted: '#70777F',
  nav: '#BAC0C4', track: '#39333F', step: '#27232D', thumbBorder: '#0B0B0E'};
const lightUi = {shell: '#E8EEF4', sidebar: 'rgba(241, 245, 249, 0.98)', border: '#C7D2DE',
  card: 'rgba(250, 252, 254, 0.98)', cardBorder: '#C5D0DC', control: '#E2EAF2',
  input: '#EDF2F7', primary: '#17212B', secondary: '#536170', muted: '#718091',
  nav: '#344251', track: '#CBD6E1', step: '#DCE5EE', thumbBorder: '#F7FAFC'};
const ThemeContext = React.createContext(darkUi);

class ScreenBoundary extends React.Component<{section: Section; children: React.ReactNode}, {failed: boolean}> {
  static contextType = ThemeContext;
  context: React.ContextType<typeof ThemeContext> = darkUi;
  state = {failed: false};
  static getDerivedStateFromError() { return {failed: true}; }
  componentDidUpdate(previous: Readonly<{section: Section}>) {
    if (previous.section !== this.props.section && this.state.failed) this.setState({failed: false});
  }
  render() {
    const ui = this.context;
    return this.state.failed
      ? <View style={[s.card, {backgroundColor: ui.card, borderColor: ui.cardBorder}]}>
          <Text style={[s.cardTitle, {color: ui.primary}]}>This screen could not render</Text>
          <Text style={[s.hint, {color: ui.secondary}]}>Your saved settings are safe. Switch sections or restart Control Center.</Text></View>
      : this.props.children;
  }
}

function accentColor(value?: string) {
  const hex = value?.trim().replace(/^#/, '') ?? '';
  return /^(?:[\da-f]{6}|[\da-f]{8})$/i.test(hex) ? `#${hex.slice(-6)}` : defaultAccent;
}

function tint(hex: string, opacity: number) {
  const red = parseInt(hex.slice(1, 3), 16);
  const green = parseInt(hex.slice(3, 5), 16);
  const blue = parseInt(hex.slice(5, 7), 16);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}

function Segments({options, value, onChange, accent}: {options: string[]; value: number; onChange: (v: number) => void; accent: string}) {
  const ui = React.useContext(ThemeContext);
  return <View style={[s.segments, {backgroundColor: ui.control, borderColor: ui.cardBorder}]}>{options.map((option, i) =>
    <Pressable key={option} accessibilityRole="button" accessibilityState={{selected: value === i}}
      onPress={() => onChange(i)} style={[s.segment, value === i && {backgroundColor: tint(accent, 0.2)}]}>
      <Text style={[s.segmentText, {color: ui.secondary}, value === i && {color: accent}]}>{option}</Text>
    </Pressable>)}</View>;
}

function ChoiceGrid({options, value, onChange, accent}: {
  options: string[]; value: number; onChange: (v: number) => void; accent: string;
}) {
  const ui = React.useContext(ThemeContext);
  return <View style={s.choiceGrid}>{options.map((option, index) =>
    <Pressable key={option} onPress={() => onChange(index)} accessibilityRole="button"
      accessibilityState={{selected: value === index}}
      style={[s.choice, {backgroundColor: ui.control, borderColor: ui.cardBorder},
        value === index && {backgroundColor: tint(accent, 0.16), borderColor: accent}]}>
      <Text style={[s.choiceText, {color: value === index ? accent : ui.primary}]}>{option}</Text>
    </Pressable>)}</View>;
}

function Field({label, value, onChange}: {label: string; value: string; onChange: (v: string) => void}) {
  const ui = React.useContext(ThemeContext);
  return <View style={s.field}><Text style={[s.label, {color: ui.primary}]}>{label}</Text>
    <TextInput accessibilityLabel={label} value={value} onChangeText={onChange}
      autoCapitalize="none" autoCorrect={false} selectionColor="#65D7B5"
      style={[s.input, {backgroundColor: ui.input, borderColor: ui.cardBorder, color: ui.primary}]}/>
  </View>;
}

const colorPresets = ['#6ED6A9', '#B68CFF', '#6EA8FF', '#53D8FB', '#FFB84D', '#FF6B8A', '#F5F5F5', '#17181D'];

function argbColor(value: string) {
  const normalized = accentColor(value).slice(1).toUpperCase();
  return `#FF${normalized}`;
}

function ColorField({label, value, accent, onChange}: {
  label: string; value: string; accent: string; onChange: (v: string) => void;
}) {
  const ui = React.useContext(ThemeContext);
  const selected = accentColor(value);
  return <View style={s.colorField}>
    <View style={s.between}><Text style={[s.label, {color: ui.primary}]}>{label}</Text>
      <View style={[s.colorPreview, {backgroundColor: selected, shadowColor: selected}]} /></View>
    <View style={s.colorRow}>
      <TextInput accessibilityLabel={label} value={value} onChangeText={onChange}
        autoCapitalize="characters" autoCorrect={false} selectionColor={accent}
        style={[s.input, s.colorInput, {backgroundColor: ui.input, borderColor: ui.cardBorder, color: ui.primary}]}/>
      <View style={s.palette}>{colorPresets.map(color =>
        <Pressable key={color} accessibilityRole="button" accessibilityLabel={`${label} ${color}`}
          accessibilityState={{selected: selected.toUpperCase() === color}}
          onPress={() => onChange(argbColor(color))}
          style={[s.swatch, {backgroundColor: color}, selected.toUpperCase() === color && {borderColor: accent, borderWidth: 3}]} />
      )}</View>
    </View>
  </View>;
}

function ToggleRow({label, hint, value, accent, onChange}: {
  label: string; hint: string; value: boolean; accent: string; onChange: (v: boolean) => void;
}) {
  const ui = React.useContext(ThemeContext);
  return <View style={[s.toggleRow, {borderTopColor: ui.cardBorder}]}><View style={s.toggleCopy}>
    <Text style={[s.toggleLabel, {color: ui.primary}]}>{label}</Text><Text style={[s.toggleHint, {color: ui.secondary}]}>{hint}</Text>
  </View><Switch value={value} trackColor={{false: ui.track, true: accent}}
      thumbColor="#F7F8F8" onValueChange={onChange}/></View>;
}

function RangeSlider({label, value, min, max, step, buttonStep, unit, accent, onChange}: {
  label: string; value: number; min: number; max: number; step: number;
  buttonStep: number; unit: string; accent: string; onChange: (value: number) => void;
}) {
  const ui = React.useContext(ThemeContext);
  const [trackWidth, setTrackWidth] = useState(1);
  const percentage = Math.max(0, Math.min(100, ((value - min) / (max - min)) * 100));
  const clamp = (next: number) => Math.max(min, Math.min(max, min + Math.round((next - min) / step) * step));
  const accessibilityScale = step < 1 ? 100 : 1;
  const setFromPointer = (locationX: number) => {
    onChange(clamp(min + (Math.max(0, Math.min(trackWidth, locationX)) / trackWidth) * (max - min)));
  };
  return <View style={s.rangeGroup}>
    <View style={s.between}><Text style={[s.label, {color: ui.primary}]}>{label}</Text>
      <Text style={[s.rangeValue, {color: accent}]}>{unit === '%' ? Math.round(value * 100) : value}{unit}</Text>
    </View>
    <View style={s.rangeRow}>
      <Pressable accessibilityRole="button" accessibilityLabel={`Decrease ${label}`}
        onPress={() => onChange(clamp(value - buttonStep))} style={[s.step, {backgroundColor: ui.step}]}>
        <Text style={[s.stepText, {color: ui.primary}]}>−</Text></Pressable>
      <View style={s.sliderHitArea}
        onLayout={event => setTrackWidth(Math.max(1, event.nativeEvent.layout.width))}
        onStartShouldSetResponder={() => true} onMoveShouldSetResponder={() => true}
        onResponderGrant={event => setFromPointer(event.nativeEvent.locationX)}
        onResponderMove={event => setFromPointer(event.nativeEvent.locationX)}
        onResponderTerminationRequest={() => false}
        accessible accessibilityRole="adjustable" accessibilityLabel={label}
        accessibilityValue={{min: Math.round(min * accessibilityScale),
          max: Math.round(max * accessibilityScale), now: Math.round(value * accessibilityScale)}}
        accessibilityActions={[{name: 'increment'}, {name: 'decrement'}]}
        onAccessibilityAction={event => onChange(clamp(value + (event.nativeEvent.actionName === 'increment' ? buttonStep : -buttonStep)))}>
        <View pointerEvents="none" style={[s.track, {backgroundColor: ui.track}]}>
          <View style={[s.fill, {width: `${percentage}%`, backgroundColor: accent}]} />
        </View>
        <View pointerEvents="none" style={[s.thumb, {left: `${percentage}%`, backgroundColor: accent, shadowColor: accent, borderColor: ui.thumbBorder}]} />
      </View>
      <Pressable accessibilityRole="button" accessibilityLabel={`Increase ${label}`}
        onPress={() => onChange(clamp(value + buttonStep))} style={[s.step, {backgroundColor: ui.step}]}>
        <Text style={[s.stepText, {color: ui.primary}]}>+</Text></Pressable>
    </View>
  </View>;
}

function App() {
  const [section, setSection] = useState<Section>('Position');
  const [settings, setSettings] = useState<Settings | null>(null);
  const [status, setStatus] = useState('Connecting to the island…');
  const [saving, setSaving] = useState(false);
  const [hoveredSection, setHoveredSection] = useState<Section | null>(null);
  const accent = accentColor(settings?.accentColor);
  const ui = settings?.theme === 1 ? lightUi : darkUi;
  const themedCard = [s.card, {backgroundColor: ui.card, borderColor: ui.cardBorder}];
  const themedTitle = [s.cardTitle, {color: ui.primary}];
  const themedHint = [s.hint, {color: ui.secondary}];
  const load = useCallback(async (): Promise<boolean> => {
    try {
      const response = await fetch(endpoint, {headers});
      if (!response.ok) throw new Error(String(response.status));
      const incoming = await response.json() as Settings;
      setSettings({...incoming, panelWidth: incoming.panelWidth || 650,
        panelHeight: incoming.panelHeight || 400,
        appPanelWidth: incoming.appPanelWidth || 920,
        appPanelHeight: incoming.appPanelHeight || 600,
        useAlbumArtBackdrop: incoming.useAlbumArtBackdrop ?? true,
        useMediaArtworkColors: incoming.useMediaArtworkColors ?? true,
        useAudioReactiveEffects: incoming.useAudioReactiveEffects ?? true,
        minimizedAppearance: incoming.minimizedAppearance ?? 0,
        compactContent: incoming.compactContent ?? 0,
        hoverContent: incoming.hoverContent ?? 0,
        expandedPanel: incoming.expandedPanel ?? 0,
        notificationBehavior: incoming.notificationBehavior ?? 0,
        notificationRules: incoming.notificationRules ?? []});
      setStatus('Connected · changes update the running island');
      return true;
    } catch {
      setStatus('Island unavailable · retrying connection…');
      return false;
    }
  }, []);
  useEffect(() => {
    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    const connect = async () => {
      const connected = await load();
      if (!connected && !disposed) retryTimer = setTimeout(connect, 2500);
    };
    void connect();
    return () => {
      disposed = true;
      if (retryTimer) clearTimeout(retryTimer);
    };
  }, [load]);
  function change<K extends keyof Settings>(key: K, value: Settings[K]) {
    setSettings(previous => previous ? {...previous, [key]: value} : previous);
    setStatus('Unsaved changes · Save changes to apply');
  }
  async function save() {
    if (!settings) return;
    setSaving(true); setStatus('Saving…');
    try {
      const response = await fetch(endpoint, {method: 'PUT', headers, body: JSON.stringify(settings)});
      if (!response.ok) throw new Error(await response.text());
      setSettings(await response.json() as Settings);
      setStatus('Saved · island updated immediately');
    } catch (error) { setStatus(`Could not save: ${String(error)}`); }
    finally { setSaving(false); }
  }
  async function command(path: string, working: string) {
    setStatus(working);
    try {
      const response = await fetch(`http://127.0.0.1:38691${path}`, {method: 'POST', headers});
      if (!response.ok) throw new Error(await response.text());
      const updated = await response.json() as Settings;
      setSettings(previous => previous ? {...previous,
        hasCustomMiniLogo: updated.hasCustomMiniLogo,
        notificationsEnabled: updated.notificationsEnabled,
        notificationStatus: updated.notificationStatus} : updated);
      setStatus(path === '/api/logo/editor' ? 'Picture editor closed · settings updated' : 'Command completed');
    } catch (error) { setStatus(`Could not complete action: ${String(error)}`); }
  }
  const meta = sectionMeta[section];
  return <ThemeContext.Provider value={ui}><View style={[s.shell, {backgroundColor: ui.shell}]}>
    <View pointerEvents="none" style={[s.ambientOrb, s.ambientOrbTop, {backgroundColor: tint(accent, 0.13)}]} />
    <View pointerEvents="none" style={[s.ambientOrb, s.ambientOrbBottom, {backgroundColor: tint(accent, 0.07)}]} />
    <View style={[s.sidebar, {backgroundColor: ui.sidebar, borderRightColor: ui.border}]}>
      <View style={s.brandRow}>
        <View style={[s.mark, {backgroundColor: accent, shadowColor: accent}]}><Text style={s.markText}>DI</Text></View>
        <View><Text style={[s.brand, {color: ui.primary}]}>Island</Text><Text style={[s.brandSub, {color: ui.muted}]}>CONTROL CENTER</Text></View>
      </View>
      <View style={s.nav}>{(['Position', 'Notch', 'Appearance', 'Alerts', 'Storage'] as Section[]).map(item =>
        <Pressable key={item} onPress={() => setSection(item)}
          onHoverIn={() => setHoveredSection(item)} onHoverOut={() => setHoveredSection(null)}
          accessibilityRole="button" accessibilityState={{selected: section === item}}
          style={[s.navItem, hoveredSection === item && {backgroundColor: tint(accent, 0.11)},
            section === item && {backgroundColor: tint(accent, 0.19)}]}>
          <View style={[s.navGlyph, {backgroundColor: ui.control}, section === item && {backgroundColor: tint(accent, 0.22)}]}>
            <Text style={[s.navGlyphText, {color: ui.muted}, (section === item || hoveredSection === item) && {color: accent}]}>{sectionMeta[item].glyph}</Text>
          </View>
          <View style={s.navCopy}><Text style={[s.navText, {color: ui.nav}, (section === item || hoveredSection === item) && {color: accent}]}>{item}</Text>
            <Text style={[s.navHint, {color: ui.muted}]}>{sectionMeta[item].summary}</Text></View>
        </Pressable>)}</View>
      <View style={s.sidebarStatus}><View style={[s.statusDot, {backgroundColor: settings ? accent : '#686970'}]} />
        <Text style={[s.sidebarStatusText, {color: ui.muted}]}>{settings ? 'ISLAND CONNECTED' : 'CONNECTING'}</Text></View>
    </View>
    <View style={s.content}>
      <View style={s.topbar}><View><Text style={[s.eyebrow, {color: accent}]}>DYNAMIC ISLAND / CONFIGURATION</Text>
        <Text style={[s.title, {color: ui.primary}]}>{section}</Text><Text style={[s.titleSub, {color: ui.secondary}]}>{meta.summary}</Text></View>
        <Pressable onPress={save} disabled={!settings || saving}
          style={[s.save, {backgroundColor: accent, shadowColor: accent}, (!settings || saving) && s.disabled]}>
          <Text style={s.saveText}>{saving ? 'Saving…' : 'Save changes'}</Text>
        </Pressable></View>
      <View style={[s.connectionBar, {backgroundColor: ui.control, borderColor: ui.border}]}><View style={[s.connectionDot, {backgroundColor: settings ? accent : '#7D7E86'}]} />
        <Text style={[s.status, {color: ui.secondary}]}>{status}</Text></View>
      {!settings ? <View style={s.empty}><ActivityIndicator color={accent}/>
        <Pressable onPress={load} style={s.reconnect}>
          <Text style={[s.link, {color: accent}]}>Reconnect</Text></Pressable></View>
      : <ScrollView style={s.scroll} contentContainerStyle={s.scrollInner}>
        <ScreenBoundary section={section}>
        <View style={[s.sectionHero, {backgroundColor: ui.card, borderColor: tint(accent, 0.28)}]}>
          <View style={[s.heroGlyph, {backgroundColor: tint(accent, 0.16)}]}><Text style={[s.heroGlyphText, {color: accent}]}>{meta.glyph}</Text></View>
          <View style={s.flex}><Text style={[s.heroTitle, {color: ui.primary}]}>{section}</Text><Text style={[s.heroText, {color: ui.secondary}]}>{meta.summary}</Text></View>
          <View style={s.heroPips}><View style={[s.heroPip, {backgroundColor: accent}]} />
            <View style={[s.heroPip, {backgroundColor: ui.border}]} /><View style={[s.heroPip, {backgroundColor: ui.border}]} /></View>
        </View>
        {section === 'Position' && <>
          <Text style={[s.lead, {color: ui.secondary}]}>Keep the notch exactly where it belongs, then tune each workspace independently.</Text>
          <View style={themedCard}><Text style={themedTitle}>Screen edge</Text>
            <Text style={themedHint}>Attach to the top, left, or right edge.</Text>
            <Segments options={['Top', 'Left', 'Right']} value={settings.edge} accent={accent}
              onChange={v => change('edge', v)}/></View>
          <View style={themedCard}><Text style={themedTitle}>Alignment</Text>
            <Segments options={['Start', 'Center', 'End']} value={settings.alignment} accent={accent}
              onChange={v => change('alignment', v)}/></View>
          <View style={themedCard}><Text style={themedTitle}>Fine adjustment</Text>
            <Text style={themedHint}>Drag a slider, or use the buttons for 10-pixel steps.</Text>
            <RangeSlider label="Horizontal" value={settings.horizontalOffset} min={-500} max={500}
              step={1} buttonStep={10} unit=" px" accent={accent}
              onChange={v => change('horizontalOffset', v)}/>
            <RangeSlider label="Vertical" value={settings.verticalOffset} min={-500} max={500}
              step={1} buttonStep={10} unit=" px" accent={accent}
              onChange={v => change('verticalOffset', v)}/>
            <Pressable onPress={() => {
              setSettings({...settings, edge: 0, alignment: 1, horizontalOffset: 0, verticalOffset: 0});
              setStatus('Unsaved changes · Save changes to apply');
            }} style={s.textButton}>
              <Text style={[s.link, {color: accent}]}>Reset to top center</Text></Pressable></View>
          <View style={s.cardGrid}>
            <View style={[s.card, s.gridCard, {backgroundColor: ui.card, borderColor: ui.cardBorder}]}><View style={s.cardHeadingRow}>
              <View style={[s.cardIcon, {backgroundColor: tint(accent, 0.14)}]}><Text style={[s.cardIconText, {color: accent}]}>▭</Text></View>
              <View><Text style={themedTitle}>Dashboard size</Text><Text style={[s.cardKicker, {color: ui.muted}]}>MEDIA · FILES · SETTINGS</Text></View>
            </View>
              <Text style={themedHint}>The compact size shared by every regular panel.</Text>
              <RangeSlider label="Width" value={settings.panelWidth} min={540} max={760}
                step={1} buttonStep={10} unit=" px" accent={accent}
                onChange={v => change('panelWidth', v)}/>
              <RangeSlider label="Height" value={settings.panelHeight} min={330} max={500}
                step={1} buttonStep={10} unit=" px" accent={accent}
                onChange={v => change('panelHeight', v)}/>
              <Pressable onPress={() => {change('panelWidth', 650); change('panelHeight', 400);}}
                style={s.textButton}><Text style={[s.link, {color: accent}]}>Reset dashboard</Text></Pressable>
            </View>
            <View style={[s.card, s.gridCard, {backgroundColor: ui.card, borderColor: tint(accent, 0.32)}]}><View style={s.cardHeadingRow}>
              <View style={[s.cardIcon, {backgroundColor: tint(accent, 0.18)}]}><Text style={[s.cardIconText, {color: accent}]}>□</Text></View>
              <View><Text style={themedTitle}>Apps workspace</Text><Text style={[s.cardKicker, {color: accent}]}>RESIZABLE IN THE ISLAND</Text></View>
            </View>
              <Text style={themedHint}>Drag its lower-right grip, or set an exact size here.</Text>
              <RangeSlider label="Width" value={settings.appPanelWidth} min={720} max={1280}
                step={1} buttonStep={20} unit=" px" accent={accent}
                onChange={v => change('appPanelWidth', v)}/>
              <RangeSlider label="Height" value={settings.appPanelHeight} min={480} max={800}
                step={1} buttonStep={20} unit=" px" accent={accent}
                onChange={v => change('appPanelHeight', v)}/>
              <Pressable onPress={() => {change('appPanelWidth', 920); change('appPanelHeight', 600);}}
                style={s.textButton}><Text style={[s.link, {color: accent}]}>Reset Apps workspace</Text></Pressable>
            </View>
          </View>
        </>}
        {section === 'Notch' && <>
          <Text style={[s.lead, {color: ui.secondary}]}>Choose exactly what the island shows at each stage.</Text>
          <View style={themedCard}><Text style={themedTitle}>Minimized notch</Text>
            <Text style={themedHint}>Smart prioritizes an active focus timer, then media, then the activity summary.</Text>
            <ChoiceGrid options={['Smart', 'Idle style', 'Music', 'Focus', 'Weather', 'System', 'Activity', 'Controls', 'Webcam']}
              value={settings.compactContent} accent={accent} onChange={v => change('compactContent', v)}/></View>
          <View style={themedCard}><Text style={themedTitle}>Hover phase</Text>
            <Text style={themedHint}>Webcam opens a live camera card only while the hover preview is visible.</Text>
            <ChoiceGrid options={['Smart', 'Off', 'Music', 'Focus', 'Weather', 'System', 'Activity', 'Controls', 'Webcam']}
              value={settings.hoverContent} accent={accent} onChange={v => change('hoverContent', v)}/></View>
          <View style={themedCard}><Text style={themedTitle}>Panel opened on click</Text>
            <Text style={themedHint}>Remember keeps your last panel. Any other choice becomes the opening dashboard.</Text>
            <ChoiceGrid options={['Remember', 'Music', 'Weather', 'System', 'Activity', 'Controls', 'Focus', 'Files', 'Clipboard', 'Apps']}
              value={settings.expandedPanel} accent={accent} onChange={v => change('expandedPanel', v)}/></View>
        </>}
        {section === 'Appearance' && <>
          <Text style={[s.lead, {color: ui.secondary}]}>A quieter surface with your colors.</Text>
          <View style={themedCard}><Text style={themedTitle}>Minimized style</Text>
            <Text style={themedHint}>Use the subtle top-edge line, or keep the earlier compact notch.</Text>
            <Segments options={['Edge line', 'Classic notch']} value={settings.minimizedAppearance} accent={accent}
              onChange={v => change('minimizedAppearance', v)}/></View>
          <View style={themedCard}><Text style={themedTitle}>Theme</Text>
            <Segments options={['Dark', 'Light']} value={settings.theme} accent={accent}
              onChange={v => {
                setSettings(previous => previous ? {...previous, theme: v,
                  backgroundColor: v === 1 && previous.backgroundColor.toUpperCase() === '#FF111113' ? '#FFE9EEF3'
                    : v === 0 && previous.backgroundColor.toUpperCase() === '#FFE9EEF3' ? '#FF111113' : previous.backgroundColor} : previous);
                setStatus('Unsaved changes · Save changes to apply');
              }}/></View>
          <View style={themedCard}><Text style={themedTitle}>Surface</Text>
            <Segments options={['Solid', 'Gradient']} value={settings.surfaceStyle} accent={accent}
              onChange={v => change('surfaceStyle', v)}/>
            <RangeSlider label="Opacity" value={settings.surfaceOpacity} min={0.1} max={1}
              step={0.01} buttonStep={0.05} unit="%" accent={accent}
              onChange={v => change('surfaceOpacity', Math.round(v * 100) / 100)}/>
            <ColorField label="Solid background" value={settings.backgroundColor ?? '#FF111113'} accent={accent}
              onChange={v => change('backgroundColor', v)}/>
            {settings.surfaceStyle === 1 && <>
              <View style={s.row}><View style={s.half}><ColorField label="Gradient start"
                value={settings.gradientStartColor}
                accent={accent} onChange={v => change('gradientStartColor', v)}/></View>
                <View style={s.half}><ColorField label="Gradient end"
                  value={settings.gradientEndColor}
                  accent={accent} onChange={v => change('gradientEndColor', v)}/></View></View>
              <Segments options={['Vertical', 'Horizontal', 'Diagonal']}
                value={settings.gradientDirection} accent={accent}
                onChange={v => change('gradientDirection', v)}/></>}
          </View>
          <View style={themedCard}><Text style={themedTitle}>Accent and mini icon</Text>
            <ColorField label="Island accent" value={settings.accentColor} accent={accent}
              onChange={v => change('accentColor', v)}/>
            <Segments options={['Letter', 'Music', 'Dot', 'Album', 'Brand']}
              value={settings.logoStyle} accent={accent} onChange={v => change('logoStyle', v)}/>
            <Text style={themedHint}>{settings.hasCustomMiniLogo
              ? 'Custom picture saved' : 'No custom picture selected'}</Text>
            <View style={s.actionRow}>
              <Pressable accessibilityRole="button" onPress={() => command('/api/logo/editor', 'Opening picture editor…')}
                style={[s.smallAction, {borderColor: accent}]}><Text style={[s.link, {color: accent}]}>Choose and crop picture</Text></Pressable>
              <Pressable accessibilityRole="button" disabled={!settings.hasCustomMiniLogo}
                onPress={() => command('/api/logo/clear', 'Removing custom picture…')}
                style={[s.smallAction, !settings.hasCustomMiniLogo && s.disabled]}>
                <Text style={[s.link, {color: ui.primary}]}>Remove</Text></Pressable>
            </View>
          </View>
          <View style={themedCard}><Text style={themedTitle}>Media atmosphere</Text>
            <Text style={themedHint}>Turn each music-driven effect on or off independently.</Text>
            <ToggleRow label="Faded cover backdrop" hint="Blend album artwork behind the media controls."
              value={settings.useAlbumArtBackdrop} accent={accent}
              onChange={v => change('useAlbumArtBackdrop', v)}/>
            <ToggleRow label="Match the island to artwork" hint="Tint the notch using the playing track’s dominant color."
              value={settings.useMediaArtworkColors} accent={accent}
              onChange={v => change('useMediaArtworkColors', v)}/>
            <ToggleRow label="Audio-reactive waves" hint="Pulse the artwork tint and waveform from the live Windows output level."
              value={settings.useAudioReactiveEffects} accent={accent}
              onChange={v => change('useAudioReactiveEffects', v)}/>
          </View>
        </>}
        {section === 'Alerts' && <>
          <Text style={[s.lead, {color: ui.secondary}]}>Decide how alerts behave globally, then override individual apps or people.</Text>
          <View style={themedCard}><Text style={themedTitle}>Windows notification access</Text>
            <Text style={themedHint}>{settings.notificationStatus || 'Check the island notification settings.'}</Text>
            <Pressable accessibilityRole="button" disabled={!settings.notificationsSupported}
              onPress={() => command('/api/notifications/enable', 'Requesting Windows notification access…')}
              style={[s.smallAction, {borderColor: ui.cardBorder}, !settings.notificationsSupported && s.disabled]}>
              <Text style={[s.link, {color: ui.primary}]}>Enable alerts</Text></Pressable></View>
          <View style={themedCard}><Text style={themedTitle}>Default alert style</Text>
            <Segments options={['Standard', 'Compact', 'Silent']} value={settings.notificationBehavior}
              accent={accent} onChange={v => change('notificationBehavior', v)}/></View>
          <View style={themedCard}><View style={s.between}><View><Text style={themedTitle}>App and person rules</Text>
            <Text style={themedHint}>Match an app name or notification title, then choose its behavior and color.</Text></View>
            <Pressable onPress={() => change('notificationRules', [...settings.notificationRules,
              {match: '', behavior: 0, accentColor: settings.accentColor}])}
              style={[s.smallAction, {borderColor: accent}]}><Text style={[s.link, {color: accent}]}>Add rule</Text></Pressable></View>
            {settings.notificationRules.map((rule, index) => <View key={index} style={[s.rule, {borderColor: ui.cardBorder}]}>
              <Field label="App or person" value={rule.match} onChange={value => change('notificationRules',
                settings.notificationRules.map((item, i) => i === index ? {...item, match: value} : item))}/>
              <Segments options={['Standard', 'Compact', 'Silent']} value={rule.behavior} accent={accent}
                onChange={value => change('notificationRules', settings.notificationRules.map((item, i) =>
                  i === index ? {...item, behavior: value} : item))}/>
              <ColorField label="Alert accent" value={rule.accentColor || settings.accentColor} accent={accent}
                onChange={value => change('notificationRules', settings.notificationRules.map((item, i) =>
                  i === index ? {...item, accentColor: value} : item))}/>
              <Pressable onPress={() => change('notificationRules', settings.notificationRules.filter((_, i) => i !== index))}
                style={s.textButton}><Text style={[s.link, {color: '#D65E6D'}]}>Remove rule</Text></Pressable>
            </View>)}
          </View>
        </>}
        {section === 'Storage' && <>
          <Text style={[s.lead, {color: ui.secondary}]}>Keep docked files safe. Send copies to TempVault.</Text>
          <View style={themedCard}>
            <View style={s.between}><View><Text style={themedTitle}>TempVault</Text>
              <Text style={themedHint}>{settings.tempVaultConnected
                ? 'Connected · Inbox found' : 'Not connected · choose an existing storage root'}</Text>
              </View><View style={[s.dot, settings.tempVaultConnected && {backgroundColor: accent}]}/></View>
            <Field label="Storage root (contains Inbox)" value={settings.tempVaultRoot}
              onChange={v => change('tempVaultRoot', v)}/>
            <View style={s.between}><View style={s.flex}>
              <Text style={[s.label, {color: ui.primary}]}>Copy new dock files automatically</Text>
              <Text style={themedHint}>Dock copies stay until you remove them.</Text></View>
              <Switch value={settings.tempVaultAutoCopy} trackColor={{false: ui.track, true: accent}}
                onValueChange={v => change('tempVaultAutoCopy', v)}/></View>
          </View><Text style={[s.footnote, {color: ui.secondary}]}>Send individual files with the TempVault button in the island’s Files panel.</Text>
        </>}
        </ScreenBoundary>
      </ScrollView>}
    </View>
  </View></ThemeContext.Provider>;
}

const s = StyleSheet.create({
  shell: {flex: 1, flexDirection: 'row', backgroundColor: '#090A0E', overflow: 'hidden'},
  ambientOrb: {position: 'absolute', width: 360, height: 360, borderRadius: 180, opacity: 0.42},
  ambientOrbTop: {right: -170, top: -210},
  ambientOrbBottom: {left: 80, bottom: -300},
  sidebar: {width: 242, backgroundColor: 'rgba(6, 7, 10, 0.94)', borderRightWidth: 1,
    borderRightColor: '#252731', paddingHorizontal: 18, paddingTop: 25},
  brandRow: {flexDirection: 'row', alignItems: 'center', gap: 11},
  mark: {width: 40, height: 40, borderRadius: 13, alignItems: 'center', justifyContent: 'center',
    shadowOpacity: 0.35, shadowRadius: 14, shadowOffset: {width: 0, height: 0}},
  markText: {fontSize: 12, color: '#090A0E', fontWeight: '900', letterSpacing: -0.5},
  brand: {fontSize: 18, color: '#F5F7F6', fontWeight: '700'},
  brandSub: {fontSize: 8, color: '#777F87', letterSpacing: 1.6, marginTop: 2},
  nav: {marginTop: 38, gap: 7},
  navItem: {minHeight: 58, paddingVertical: 9, paddingHorizontal: 10, borderRadius: 12,
    flexDirection: 'row', alignItems: 'center', cursor: 'pointer'},
  navGlyph: {width: 34, height: 34, borderRadius: 10, backgroundColor: '#161820',
    alignItems: 'center', justifyContent: 'center', marginRight: 10},
  navGlyphText: {color: '#899097', fontFamily: 'Segoe Fluent Icons', fontSize: 17, fontWeight: '400'},
  navCopy: {flex: 1},
  navText: {color: '#BAC0C4', fontSize: 13, fontWeight: '700'},
  navHint: {color: '#646B72', fontSize: 8.5, marginTop: 2},
  sidebarStatus: {position: 'absolute', left: 20, bottom: 23, flexDirection: 'row', alignItems: 'center'},
  statusDot: {width: 7, height: 7, borderRadius: 4, marginRight: 8},
  sidebarStatusText: {color: '#70777E', fontSize: 8.5, letterSpacing: 1.15, fontWeight: '700'},
  content: {flex: 1, paddingTop: 27, paddingHorizontal: 30},
  topbar: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center'},
  eyebrow: {fontSize: 10, letterSpacing: 2, fontWeight: '700'},
  title: {color: '#F4F6F5', fontSize: 27, fontWeight: '700', marginTop: 3},
  titleSub: {color: '#7F878E', fontSize: 10.5, marginTop: 2},
  connectionBar: {height: 29, alignSelf: 'flex-start', flexDirection: 'row', alignItems: 'center',
    backgroundColor: '#111319', borderWidth: 1, borderColor: '#252832', borderRadius: 9,
    paddingHorizontal: 10, marginTop: 12, marginBottom: 13},
  connectionDot: {width: 6, height: 6, borderRadius: 3, marginRight: 7},
  status: {color: '#90989E', fontSize: 10},
  save: {borderRadius: 10, paddingHorizontal: 18, paddingVertical: 10, cursor: 'pointer',
    shadowOpacity: 0.22, shadowRadius: 14, shadowOffset: {width: 0, height: 3}},
  saveText: {color: '#0B0B0E', fontSize: 12, fontWeight: '700'},
  disabled: {opacity: 0.45},
  scroll: {flex: 1}, scrollInner: {paddingBottom: 36, maxWidth: 880},
  sectionHero: {height: 76, flexDirection: 'row', alignItems: 'center', backgroundColor: '#12141A',
    borderWidth: 1, borderRadius: 16, paddingHorizontal: 16, marginBottom: 15},
  heroGlyph: {width: 43, height: 43, borderRadius: 13, alignItems: 'center', justifyContent: 'center', marginRight: 13},
  heroGlyphText: {fontFamily: 'Segoe Fluent Icons', fontSize: 22, fontWeight: '400'},
  heroTitle: {color: '#F1F3F4', fontSize: 14, fontWeight: '700'},
  heroText: {color: '#818990', fontSize: 10, marginTop: 3},
  heroPips: {flexDirection: 'row', gap: 5, alignItems: 'center'},
  heroPip: {width: 5, height: 5, borderRadius: 3, backgroundColor: '#353841'},
  lead: {color: '#AEB6BA', fontSize: 12, marginBottom: 13},
  card: {backgroundColor: 'rgba(20, 21, 27, 0.96)', borderWidth: 1, borderColor: '#2B2E38',
    borderRadius: 15, padding: 17, marginBottom: 11},
  cardGrid: {flexDirection: 'row', gap: 11, alignItems: 'stretch'},
  gridCard: {flex: 1},
  cardHeadingRow: {flexDirection: 'row', alignItems: 'center', marginBottom: 7},
  cardIcon: {width: 32, height: 32, borderRadius: 9, alignItems: 'center', justifyContent: 'center', marginRight: 9},
  cardIconText: {fontSize: 17, fontWeight: '700'},
  cardKicker: {color: '#70777F', fontSize: 7.5, letterSpacing: 0.8, marginTop: 1},
  cardTitle: {color: '#F1F4F2', fontSize: 15, fontWeight: '700', marginBottom: 4},
  hint: {color: '#909B9E', fontSize: 11, marginBottom: 9, lineHeight: 17},
  label: {color: '#D5DCDA', fontSize: 12, fontWeight: '600', marginBottom: 7},
  segments: {flexDirection: 'row', backgroundColor: '#0B0A0D', borderRadius: 9,
    borderWidth: 1, borderColor: '#322C39', padding: 3, marginTop: 7},
  segment: {flex: 1, alignItems: 'center', paddingVertical: 9, borderRadius: 7, cursor: 'pointer'},
  segmentText: {color: '#9FAAAC', fontSize: 11, fontWeight: '600'},
  choiceGrid: {flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 9},
  choice: {width: '31%', minWidth: 118, paddingHorizontal: 11, paddingVertical: 10,
    borderWidth: 1, borderRadius: 9, cursor: 'pointer'},
  choiceText: {fontSize: 11, fontWeight: '700', textAlign: 'center'},
  field: {marginTop: 12, marginBottom: 4},
  input: {backgroundColor: '#0B0A0D', borderColor: '#39313F', borderWidth: 1,
    borderRadius: 8, color: '#E8EFED', paddingHorizontal: 11, paddingVertical: 8, fontSize: 12},
  colorField: {marginTop: 12, marginBottom: 5},
  colorRow: {flexDirection: 'row', alignItems: 'center', gap: 12},
  colorInput: {width: 142},
  colorPreview: {width: 24, height: 24, borderRadius: 8, borderWidth: 1, borderColor: '#FFFFFF33',
    shadowOpacity: 0.4, shadowRadius: 9, shadowOffset: {width: 0, height: 0}},
  palette: {flex: 1, flexDirection: 'row', flexWrap: 'wrap', gap: 7},
  swatch: {width: 27, height: 27, borderRadius: 8, borderWidth: 1, borderColor: '#FFFFFF24', cursor: 'pointer'},
  toggleRow: {minHeight: 58, flexDirection: 'row', alignItems: 'center', borderTopWidth: 1,
    borderTopColor: '#292C34', paddingVertical: 10},
  toggleCopy: {flex: 1, marginRight: 18},
  toggleLabel: {color: '#E7ECEA', fontSize: 12, fontWeight: '600'},
  toggleHint: {color: '#818A90', fontSize: 10, lineHeight: 15, marginTop: 3},
  row: {flexDirection: 'row', gap: 12}, half: {flex: 1},
  between: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center'},
  textButton: {alignSelf: 'flex-start', paddingTop: 12, paddingBottom: 4, cursor: 'pointer'},
  link: {fontSize: 12, fontWeight: '600'},
  rangeGroup: {marginTop: 17},
  rangeValue: {fontSize: 12, fontWeight: '700', fontVariant: ['tabular-nums']},
  rangeRow: {flexDirection: 'row', alignItems: 'center', gap: 12, marginTop: 2},
  step: {width: 34, height: 34, alignItems: 'center', justifyContent: 'center',
    backgroundColor: '#27232D', borderRadius: 8, cursor: 'pointer'},
  stepText: {color: '#E9F4EE', fontSize: 18},
  sliderHitArea: {flex: 1, height: 34, justifyContent: 'center', cursor: 'pointer'},
  track: {height: 6, backgroundColor: '#39333F', borderRadius: 4, overflow: 'hidden'},
  fill: {height: 6, borderRadius: 4},
  thumb: {position: 'absolute', width: 16, height: 16, marginLeft: -8,
    borderRadius: 8, borderWidth: 3, borderColor: '#0B0B0E', elevation: 3},
  dot: {width: 9, height: 9, borderRadius: 5, backgroundColor: '#6A6E70'},
  flex: {flex: 1, marginRight: 12},
  footnote: {color: '#8B9798', fontSize: 11, lineHeight: 17, marginTop: 4},
  actionRow: {flexDirection: 'row', gap: 10, marginTop: 8},
  smallAction: {paddingHorizontal: 13, paddingVertical: 9, borderWidth: 1,
    borderColor: '#544B5C', borderRadius: 8, alignSelf: 'flex-start', cursor: 'pointer'},
  rule: {borderTopWidth: 1, paddingTop: 12, marginTop: 12},
  empty: {flex: 1, justifyContent: 'center', alignItems: 'center', gap: 15},
  reconnect: {paddingHorizontal: 14, paddingVertical: 9, borderWidth: 1,
    borderColor: '#594975', borderRadius: 8, cursor: 'pointer'},
});
export default App;
