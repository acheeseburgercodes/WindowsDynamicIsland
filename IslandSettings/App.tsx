import React, {useCallback, useEffect, useState} from 'react';
import {ActivityIndicator, Pressable, ScrollView, StyleSheet, Switch, Text, TextInput, View} from 'react-native';

type Settings = {
  surfaceOpacity: number; edge: number; alignment: number;
  horizontalOffset: number; verticalOffset: number; theme: number;
  surfaceStyle: number; accentColor: string; backgroundColor: string; gradientStartColor: string;
  gradientEndColor: string; gradientDirection: number; logoStyle: number;
  tempVaultRoot: string; tempVaultAutoCopy: boolean; tempVaultConnected: boolean;
  panelWidth: number; panelHeight: number; notificationsSupported: boolean;
  notificationsEnabled: boolean; notificationStatus: string; hasCustomMiniLogo: boolean;
};
type Section = 'Position' | 'Appearance' | 'Storage';
const endpoint = 'http://127.0.0.1:38691/api/settings';
const headers = {'X-DynamicIsland-Settings': '1', 'Content-Type': 'application/json'};
const defaultAccent = '#B68CFF';

class ScreenBoundary extends React.Component<{section: Section; children: React.ReactNode}, {failed: boolean}> {
  state = {failed: false};
  static getDerivedStateFromError() { return {failed: true}; }
  componentDidUpdate(previous: Readonly<{section: Section}>) {
    if (previous.section !== this.props.section && this.state.failed) this.setState({failed: false});
  }
  render() {
    return this.state.failed
      ? <View style={s.card}><Text style={s.cardTitle}>This screen could not render</Text>
          <Text style={s.hint}>Your saved settings are safe. Switch sections or restart Control Center.</Text></View>
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
  return <View style={s.segments}>{options.map((option, i) =>
    <Pressable key={option} accessibilityRole="button" accessibilityState={{selected: value === i}}
      onPress={() => onChange(i)} style={[s.segment, value === i && {backgroundColor: tint(accent, 0.2)}]}>
      <Text style={[s.segmentText, value === i && {color: accent}]}>{option}</Text>
    </Pressable>)}</View>;
}

function Field({label, value, onChange}: {label: string; value: string; onChange: (v: string) => void}) {
  return <View style={s.field}><Text style={s.label}>{label}</Text>
    <TextInput accessibilityLabel={label} value={value} onChangeText={onChange}
      autoCapitalize="none" autoCorrect={false} selectionColor="#65D7B5" style={s.input}/>
  </View>;
}

function RangeSlider({label, value, min, max, step, buttonStep, unit, accent, onChange}: {
  label: string; value: number; min: number; max: number; step: number;
  buttonStep: number; unit: string; accent: string; onChange: (value: number) => void;
}) {
  const [trackWidth, setTrackWidth] = useState(1);
  const percentage = Math.max(0, Math.min(100, ((value - min) / (max - min)) * 100));
  const clamp = (next: number) => Math.max(min, Math.min(max, min + Math.round((next - min) / step) * step));
  const accessibilityScale = step < 1 ? 100 : 1;
  const setFromPointer = (locationX: number) => {
    onChange(clamp(min + (Math.max(0, Math.min(trackWidth, locationX)) / trackWidth) * (max - min)));
  };
  return <View style={s.rangeGroup}>
    <View style={s.between}><Text style={s.label}>{label}</Text>
      <Text style={[s.rangeValue, {color: accent}]}>{unit === '%' ? Math.round(value * 100) : value}{unit}</Text>
    </View>
    <View style={s.rangeRow}>
      <Pressable accessibilityRole="button" accessibilityLabel={`Decrease ${label}`}
        onPress={() => onChange(clamp(value - buttonStep))} style={s.step}>
        <Text style={s.stepText}>−</Text></Pressable>
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
        <View pointerEvents="none" style={s.track}>
          <View style={[s.fill, {width: `${percentage}%`, backgroundColor: accent}]} />
        </View>
        <View pointerEvents="none" style={[s.thumb, {left: `${percentage}%`, backgroundColor: accent, shadowColor: accent}]} />
      </View>
      <Pressable accessibilityRole="button" accessibilityLabel={`Increase ${label}`}
        onPress={() => onChange(clamp(value + buttonStep))} style={s.step}>
        <Text style={s.stepText}>+</Text></Pressable>
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
  const load = useCallback(async (): Promise<boolean> => {
    try {
      const response = await fetch(endpoint, {headers});
      if (!response.ok) throw new Error(String(response.status));
      const incoming = await response.json() as Settings;
      setSettings({...incoming, panelWidth: incoming.panelWidth || 650,
        panelHeight: incoming.panelHeight || 400});
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
      if (path !== '/api/notifications/test') {
        const updated = await response.json() as Settings;
        setSettings(previous => previous ? {...previous,
          hasCustomMiniLogo: updated.hasCustomMiniLogo,
          notificationsEnabled: updated.notificationsEnabled,
          notificationStatus: updated.notificationStatus} : updated);
      }
      setStatus(path === '/api/logo/editor' ? 'Picture editor closed · settings updated' : 'Command completed');
    } catch (error) { setStatus(`Could not complete action: ${String(error)}`); }
  }
  return <View style={s.shell}>
    <View style={s.sidebar}>
      <View style={[s.mark, {backgroundColor: accent}]}><Text style={s.markText}>◖</Text></View>
      <Text style={s.brand}>Island</Text><Text style={s.brandSub}>CONTROL CENTER</Text>
      <View style={s.nav}>{(['Position', 'Appearance', 'Storage'] as Section[]).map(item =>
        <Pressable key={item} onPress={() => setSection(item)}
          onHoverIn={() => setHoveredSection(item)} onHoverOut={() => setHoveredSection(null)}
          accessibilityRole="button" accessibilityState={{selected: section === item}}
          style={[s.navItem, hoveredSection === item && {backgroundColor: tint(accent, 0.11)},
            section === item && {backgroundColor: tint(accent, 0.19), borderLeftColor: accent}]}>
          <Text style={[s.navText, (section === item || hoveredSection === item) && {color: accent}]}>{item}</Text>
        </Pressable>)}</View>
      <Text style={s.footer}>WINDOWS DYNAMIC ISLAND</Text>
    </View>
    <View style={s.content}>
      <View style={s.topbar}><View><Text style={[s.eyebrow, {color: accent}]}>CONFIGURATION</Text>
        <Text style={s.title}>{section}</Text></View>
        <Pressable onPress={save} disabled={!settings || saving}
          style={[s.save, {backgroundColor: accent}, (!settings || saving) && s.disabled]}>
          <Text style={s.saveText}>{saving ? 'Saving…' : 'Save changes'}</Text>
        </Pressable></View>
      <Text style={s.status}>{status}</Text>
      {!settings ? <View style={s.empty}><ActivityIndicator color={accent}/>
        <Pressable onPress={load} style={s.reconnect}>
          <Text style={[s.link, {color: accent}]}>Reconnect</Text></Pressable></View>
      : <ScrollView style={s.scroll} contentContainerStyle={s.scrollInner}>
        <ScreenBoundary section={section}>
        {section === 'Position' && <>
          <Text style={s.lead}>Keep the notch exactly where it belongs.</Text>
          <View style={s.card}><Text style={s.cardTitle}>Screen edge</Text>
            <Text style={s.hint}>Attach to the top, left, or right edge.</Text>
            <Segments options={['Top', 'Left', 'Right']} value={settings.edge} accent={accent}
              onChange={v => change('edge', v)}/></View>
          <View style={s.card}><Text style={s.cardTitle}>Alignment</Text>
            <Segments options={['Start', 'Center', 'End']} value={settings.alignment} accent={accent}
              onChange={v => change('alignment', v)}/></View>
          <View style={s.card}><Text style={s.cardTitle}>Fine adjustment</Text>
            <Text style={s.hint}>Drag a slider, or use the buttons for 10-pixel steps.</Text>
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
          <View style={s.card}><Text style={s.cardTitle}>Panel size</Text>
            <Text style={s.hint}>Every dashboard, including Apps, uses this size.</Text>
            <RangeSlider label="Width" value={settings.panelWidth} min={540} max={760}
              step={1} buttonStep={10} unit=" px" accent={accent}
              onChange={v => change('panelWidth', v)}/>
            <RangeSlider label="Height" value={settings.panelHeight} min={330} max={500}
              step={1} buttonStep={10} unit=" px" accent={accent}
              onChange={v => change('panelHeight', v)}/>
            <Pressable onPress={() => {change('panelWidth', 650); change('panelHeight', 400);}}
              style={s.textButton}><Text style={[s.link, {color: accent}]}>Reset panel size</Text></Pressable>
          </View>
        </>}
        {section === 'Appearance' && <>
          <Text style={s.lead}>A quieter surface with your colors.</Text>
          <View style={s.card}><Text style={s.cardTitle}>Theme</Text>
            <Segments options={['Dark', 'Light']} value={settings.theme} accent={accent}
              onChange={v => change('theme', v)}/></View>
          <View style={s.card}><Text style={s.cardTitle}>Surface</Text>
            <Segments options={['Solid', 'Gradient']} value={settings.surfaceStyle} accent={accent}
              onChange={v => change('surfaceStyle', v)}/>
            <RangeSlider label="Opacity" value={settings.surfaceOpacity} min={0.1} max={1}
              step={0.01} buttonStep={0.05} unit="%" accent={accent}
              onChange={v => change('surfaceOpacity', Math.round(v * 100) / 100)}/>
            <Field label="Solid background color" value={settings.backgroundColor ?? '#FF111113'}
              onChange={v => change('backgroundColor', v)}/>
            {settings.surfaceStyle === 1 && <>
              <View style={s.row}><View style={s.half}><Field label="Gradient start"
                value={settings.gradientStartColor}
                onChange={v => change('gradientStartColor', v)}/></View>
                <View style={s.half}><Field label="Gradient end"
                  value={settings.gradientEndColor}
                  onChange={v => change('gradientEndColor', v)}/></View></View>
              <Segments options={['Vertical', 'Horizontal', 'Diagonal']}
                value={settings.gradientDirection} accent={accent}
                onChange={v => change('gradientDirection', v)}/></>}
          </View>
          <View style={s.card}><Text style={s.cardTitle}>Accent and mini icon</Text>
            <Field label="Accent hex color" value={settings.accentColor}
              onChange={v => change('accentColor', v)}/>
            <Segments options={['Letter', 'Music', 'Dot', 'Album', 'Brand']}
              value={settings.logoStyle} accent={accent} onChange={v => change('logoStyle', v)}/>
            <Text style={s.hint}>{settings.hasCustomMiniLogo
              ? 'Custom picture saved' : 'No custom picture selected'}</Text>
            <View style={s.actionRow}>
              <Pressable accessibilityRole="button" onPress={() => command('/api/logo/editor', 'Opening picture editor…')}
                style={[s.smallAction, {borderColor: accent}]}><Text style={[s.link, {color: accent}]}>Choose and crop picture</Text></Pressable>
              <Pressable accessibilityRole="button" disabled={!settings.hasCustomMiniLogo}
                onPress={() => command('/api/logo/clear', 'Removing custom picture…')}
                style={[s.smallAction, !settings.hasCustomMiniLogo && s.disabled]}>
                <Text style={s.link}>Remove</Text></Pressable>
            </View>
          </View>
          <View style={s.card}><Text style={s.cardTitle}>Windows notifications</Text>
            <Text style={s.hint}>{settings.notificationStatus || 'Check the island notification settings.'}</Text>
            <View style={s.actionRow}>
              <Pressable accessibilityRole="button" disabled={!settings.notificationsSupported}
                onPress={() => command('/api/notifications/enable', 'Requesting Windows notification access…')}
                style={[s.smallAction, !settings.notificationsSupported && s.disabled]}>
                <Text style={s.link}>Enable alerts</Text></Pressable>
              <Pressable accessibilityRole="button" onPress={() => command('/api/notifications/test', 'Testing notch animation…')}
                style={s.smallAction}><Text style={s.link}>Test animation</Text></Pressable>
            </View>
          </View>
        </>}
        {section === 'Storage' && <>
          <Text style={s.lead}>Keep docked files safe. Send copies to TempVault.</Text>
          <View style={s.card}>
            <View style={s.between}><View><Text style={s.cardTitle}>TempVault</Text>
              <Text style={s.hint}>{settings.tempVaultConnected
                ? 'Connected · Inbox found' : 'Not connected · choose an existing storage root'}</Text>
              </View><View style={[s.dot, settings.tempVaultConnected && {backgroundColor: accent}]}/></View>
            <Field label="Storage root (contains Inbox)" value={settings.tempVaultRoot}
              onChange={v => change('tempVaultRoot', v)}/>
            <View style={s.between}><View style={s.flex}>
              <Text style={s.label}>Copy new dock files automatically</Text>
              <Text style={s.hint}>Dock copies stay until you remove them.</Text></View>
              <Switch value={settings.tempVaultAutoCopy} trackColor={{false: '#3A3D42', true: accent}}
                onValueChange={v => change('tempVaultAutoCopy', v)}/></View>
          </View><Text style={s.footnote}>Send individual files with the TempVault button in the island’s Files panel.</Text>
        </>}
        </ScreenBoundary>
      </ScrollView>}
    </View>
  </View>;
}

const s = StyleSheet.create({
  shell: {flex: 1, flexDirection: 'row', backgroundColor: '#0B0B0E'},
  sidebar: {width: 218, backgroundColor: '#070709', borderRightWidth: 1,
    borderRightColor: '#29262F', paddingHorizontal: 18, paddingTop: 29},
  mark: {width: 37, height: 37, borderRadius: 12,
    alignItems: 'center', justifyContent: 'center'},
  markText: {fontSize: 24, color: '#0B0B0E'},
  brand: {fontSize: 21, color: '#F5F7F6', fontWeight: '700', marginTop: 14},
  brandSub: {fontSize: 10, color: '#858B90', letterSpacing: 2, marginTop: 2},
  nav: {marginTop: 41, gap: 5},
  navItem: {paddingVertical: 11, paddingHorizontal: 12, borderRadius: 9,
    borderLeftWidth: 2, borderLeftColor: 'transparent', cursor: 'pointer'},
  navText: {color: '#A5ADB0', fontSize: 14, fontWeight: '500'},
  footer: {position: 'absolute', bottom: 24, left: 18, color: '#626A6E', fontSize: 9, letterSpacing: 1},
  content: {flex: 1, paddingTop: 30, paddingHorizontal: 32},
  topbar: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center'},
  eyebrow: {fontSize: 10, letterSpacing: 2, fontWeight: '700'},
  title: {color: '#F4F6F5', fontSize: 28, fontWeight: '700', marginTop: 4},
  status: {color: '#919B9F', fontSize: 11, marginTop: 9, marginBottom: 16},
  save: {borderRadius: 9, paddingHorizontal: 17, paddingVertical: 10, cursor: 'pointer'},
  saveText: {color: '#0B0B0E', fontSize: 12, fontWeight: '700'},
  disabled: {opacity: 0.45},
  scroll: {flex: 1}, scrollInner: {paddingBottom: 36, maxWidth: 720},
  lead: {color: '#BBC4C4', fontSize: 13, marginBottom: 17},
  card: {backgroundColor: '#151419', borderWidth: 1, borderColor: '#302B36',
    borderRadius: 14, padding: 18, marginBottom: 11},
  cardTitle: {color: '#F1F4F2', fontSize: 15, fontWeight: '700', marginBottom: 4},
  hint: {color: '#909B9E', fontSize: 11, marginBottom: 9, lineHeight: 17},
  label: {color: '#D5DCDA', fontSize: 12, fontWeight: '600', marginBottom: 7},
  segments: {flexDirection: 'row', backgroundColor: '#0B0A0D', borderRadius: 9,
    borderWidth: 1, borderColor: '#322C39', padding: 3, marginTop: 7},
  segment: {flex: 1, alignItems: 'center', paddingVertical: 9, borderRadius: 7, cursor: 'pointer'},
  segmentText: {color: '#9FAAAC', fontSize: 11, fontWeight: '600'},
  field: {marginTop: 12, marginBottom: 4},
  input: {backgroundColor: '#0B0A0D', borderColor: '#39313F', borderWidth: 1,
    borderRadius: 8, color: '#E8EFED', paddingHorizontal: 11, paddingVertical: 8, fontSize: 12},
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
  empty: {flex: 1, justifyContent: 'center', alignItems: 'center', gap: 15},
  reconnect: {paddingHorizontal: 14, paddingVertical: 9, borderWidth: 1,
    borderColor: '#594975', borderRadius: 8, cursor: 'pointer'},
});
export default App;
