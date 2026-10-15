import React, {useCallback, useEffect, useState} from 'react';
import {ActivityIndicator, Pressable, ScrollView, StyleSheet, Switch, Text, TextInput, View} from 'react-native';

type Settings = {
  surfaceOpacity: number; edge: number; alignment: number;
  horizontalOffset: number; verticalOffset: number; theme: number;
  surfaceStyle: number; accentColor: string; gradientStartColor: string;
  gradientEndColor: string; gradientDirection: number; logoStyle: number;
  tempVaultRoot: string; tempVaultAutoCopy: boolean; tempVaultConnected: boolean;
};
type Section = 'Position' | 'Appearance' | 'Storage';
const endpoint = 'http://127.0.0.1:38691/api/settings';
const headers = {'X-DynamicIsland-Settings': '1', 'Content-Type': 'application/json'};

function Segments({options, value, onChange}: {options: string[]; value: number; onChange: (v: number) => void}) {
  return <View style={s.segments}>{options.map((option, i) =>
    <Pressable key={option} accessibilityRole="button" accessibilityState={{selected: value === i}}
      onPress={() => onChange(i)} style={[s.segment, value === i && s.selected]}>
      <Text style={[s.segmentText, value === i && s.selectedText]}>{option}</Text>
    </Pressable>)}</View>;
}

function Field({label, value, onChange}: {label: string; value: string; onChange: (v: string) => void}) {
  return <View style={s.field}><Text style={s.label}>{label}</Text>
    <TextInput accessibilityLabel={label} value={value} onChangeText={onChange}
      autoCapitalize="none" autoCorrect={false} selectionColor="#65D7B5" style={s.input}/>
  </View>;
}

function App() {
  const [section, setSection] = useState<Section>('Position');
  const [settings, setSettings] = useState<Settings | null>(null);
  const [status, setStatus] = useState('Connecting to the island…');
  const [saving, setSaving] = useState(false);
  const load = useCallback(async () => {
    try {
      const response = await fetch(endpoint, {headers});
      if (!response.ok) throw new Error(String(response.status));
      setSettings(await response.json() as Settings);
      setStatus('Connected · changes update the running island');
    } catch { setStatus('Island unavailable. Start DynamicIsland.exe, then reconnect.'); }
  }, []);
  useEffect(() => { load(); }, [load]);
  function change<K extends keyof Settings>(key: K, value: Settings[K]) {
    setSettings(previous => previous ? {...previous, [key]: value} : previous);
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
  return <View style={s.shell}>
    <View style={s.sidebar}>
      <View style={s.mark}><Text style={s.markText}>◖</Text></View>
      <Text style={s.brand}>Island</Text><Text style={s.brandSub}>CONTROL CENTER</Text>
      <View style={s.nav}>{(['Position', 'Appearance', 'Storage'] as Section[]).map(item =>
        <Pressable key={item} onPress={() => setSection(item)}
          style={[s.navItem, section === item && s.navActive]}>
          <Text style={[s.navText, section === item && s.navTextActive]}>{item}</Text>
        </Pressable>)}</View>
      <Text style={s.footer}>WINDOWS DYNAMIC ISLAND</Text>
    </View>
    <View style={s.content}>
      <View style={s.topbar}><View><Text style={s.eyebrow}>CONFIGURATION</Text>
        <Text style={s.title}>{section}</Text></View>
        <Pressable onPress={save} disabled={!settings || saving}
          style={[s.save, (!settings || saving) && s.disabled]}>
          <Text style={s.saveText}>{saving ? 'Saving…' : 'Save changes'}</Text>
        </Pressable></View>
      <Text style={s.status}>{status}</Text>
      {!settings ? <View style={s.empty}><ActivityIndicator color="#65D7B5"/>
        <Pressable onPress={load} style={s.reconnect}>
          <Text style={s.link}>Reconnect</Text></Pressable></View>
      : <ScrollView style={s.scroll} contentContainerStyle={s.scrollInner}>
        {section === 'Position' && <>
          <Text style={s.lead}>Keep the notch exactly where it belongs.</Text>
          <View style={s.card}><Text style={s.cardTitle}>Screen edge</Text>
            <Text style={s.hint}>Attach to the top, left, or right edge.</Text>
            <Segments options={['Top', 'Left', 'Right']} value={settings.edge}
              onChange={v => change('edge', v)}/></View>
          <View style={s.card}><Text style={s.cardTitle}>Alignment</Text>
            <Segments options={['Start', 'Center', 'End']} value={settings.alignment}
              onChange={v => change('alignment', v)}/></View>
          <View style={s.card}><Text style={s.cardTitle}>Fine adjustment</Text>
            <Text style={s.hint}>Offsets are in screen pixels, from −500 to 500.</Text>
            <View style={s.row}><View style={s.half}><Field label="Horizontal"
              value={String(settings.horizontalOffset)}
              onChange={v => change('horizontalOffset', Number(v) || 0)}/></View>
              <View style={s.half}><Field label="Vertical"
                value={String(settings.verticalOffset)}
                onChange={v => change('verticalOffset', Number(v) || 0)}/></View></View>
            <Pressable onPress={() => setSettings({...settings, edge: 0, alignment: 1,
              horizontalOffset: 0, verticalOffset: 0})} style={s.textButton}>
              <Text style={s.link}>Reset to top center</Text></Pressable></View>
        </>}
        {section === 'Appearance' && <>
          <Text style={s.lead}>A quieter surface with your colors.</Text>
          <View style={s.card}><Text style={s.cardTitle}>Theme</Text>
            <Segments options={['Dark', 'Light']} value={settings.theme}
              onChange={v => change('theme', v)}/></View>
          <View style={s.card}><Text style={s.cardTitle}>Surface</Text>
            <Segments options={['Solid', 'Gradient']} value={settings.surfaceStyle}
              onChange={v => change('surfaceStyle', v)}/>
            <Text style={[s.label, s.spaceTop]}>Opacity · {Math.round(settings.surfaceOpacity * 100)}%</Text>
            <View style={s.rowCenter}>
              <Pressable style={s.step} onPress={() => change('surfaceOpacity',
                Math.max(0.1, Math.round((settings.surfaceOpacity - 0.05) * 100) / 100))}>
                <Text style={s.stepText}>−</Text></Pressable>
              <View style={s.track}><View style={[s.fill,
                {width: `${settings.surfaceOpacity * 100}%` as `${number}%`}]}/></View>
              <Pressable style={s.step} onPress={() => change('surfaceOpacity',
                Math.min(1, Math.round((settings.surfaceOpacity + 0.05) * 100) / 100))}>
                <Text style={s.stepText}>+</Text></Pressable></View>
            {settings.surfaceStyle === 1 && <>
              <View style={s.row}><View style={s.half}><Field label="Gradient start"
                value={settings.gradientStartColor}
                onChange={v => change('gradientStartColor', v)}/></View>
                <View style={s.half}><Field label="Gradient end"
                  value={settings.gradientEndColor}
                  onChange={v => change('gradientEndColor', v)}/></View></View>
              <Segments options={['Vertical', 'Horizontal', 'Diagonal']}
                value={settings.gradientDirection}
                onChange={v => change('gradientDirection', v)}/></>}
          </View>
          <View style={s.card}><Text style={s.cardTitle}>Accent and mini icon</Text>
            <Field label="Accent hex color" value={settings.accentColor}
              onChange={v => change('accentColor', v)}/>
            <Segments options={['Letter', 'Music', 'Dot', 'Album', 'Brand']}
              value={settings.logoStyle} onChange={v => change('logoStyle', v)}/>
            <Text style={s.hint}>Custom photo cropping remains available in the island.</Text>
          </View>
        </>}
        {section === 'Storage' && <>
          <Text style={s.lead}>Keep docked files safe. Send copies to TempVault.</Text>
          <View style={s.card}>
            <View style={s.between}><View><Text style={s.cardTitle}>TempVault</Text>
              <Text style={s.hint}>{settings.tempVaultConnected
                ? 'Connected · Inbox found' : 'Not connected · choose an existing storage root'}</Text>
              </View><View style={[s.dot, settings.tempVaultConnected && s.dotOn]}/></View>
            <Field label="Storage root (contains Inbox)" value={settings.tempVaultRoot}
              onChange={v => change('tempVaultRoot', v)}/>
            <View style={s.between}><View style={s.flex}>
              <Text style={s.label}>Copy new dock files automatically</Text>
              <Text style={s.hint}>Dock copies stay until you remove them.</Text></View>
              <Switch value={settings.tempVaultAutoCopy}
                onValueChange={v => change('tempVaultAutoCopy', v)}/></View>
          </View><Text style={s.footnote}>Send individual files with the TempVault button in the island’s Files panel.</Text>
        </>}
      </ScrollView>}
    </View>
  </View>;
}

const s = StyleSheet.create({
  shell: {flex: 1, flexDirection: 'row', backgroundColor: '#101114'},
  sidebar: {width: 218, backgroundColor: '#17191D', borderRightWidth: 1,
    borderRightColor: '#2A2D32', paddingHorizontal: 18, paddingTop: 29},
  mark: {width: 37, height: 37, borderRadius: 12, backgroundColor: '#65D7B5',
    alignItems: 'center', justifyContent: 'center'},
  markText: {fontSize: 24, color: '#102018'},
  brand: {fontSize: 21, color: '#F5F7F6', fontWeight: '700', marginTop: 14},
  brandSub: {fontSize: 10, color: '#858B90', letterSpacing: 2, marginTop: 2},
  nav: {marginTop: 41, gap: 5},
  navItem: {paddingVertical: 11, paddingHorizontal: 12, borderRadius: 9},
  navActive: {backgroundColor: '#2A3733'},
  navText: {color: '#A5ADB0', fontSize: 14, fontWeight: '500'},
  navTextActive: {color: '#D9FFE9'},
  footer: {position: 'absolute', bottom: 24, left: 18, color: '#626A6E', fontSize: 9, letterSpacing: 1},
  content: {flex: 1, paddingTop: 30, paddingHorizontal: 32},
  topbar: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center'},
  eyebrow: {color: '#65D7B5', fontSize: 10, letterSpacing: 2, fontWeight: '700'},
  title: {color: '#F4F6F5', fontSize: 28, fontWeight: '700', marginTop: 4},
  status: {color: '#919B9F', fontSize: 11, marginTop: 9, marginBottom: 16},
  save: {backgroundColor: '#65D7B5', borderRadius: 9, paddingHorizontal: 17, paddingVertical: 10},
  saveText: {color: '#102018', fontSize: 12, fontWeight: '700'},
  disabled: {opacity: 0.45},
  scroll: {flex: 1}, scrollInner: {paddingBottom: 36, maxWidth: 720},
  lead: {color: '#BBC4C4', fontSize: 13, marginBottom: 17},
  card: {backgroundColor: '#1B1E22', borderWidth: 1, borderColor: '#2D3236',
    borderRadius: 14, padding: 18, marginBottom: 11},
  cardTitle: {color: '#F1F4F2', fontSize: 15, fontWeight: '700', marginBottom: 4},
  hint: {color: '#909B9E', fontSize: 11, marginBottom: 9, lineHeight: 17},
  label: {color: '#D5DCDA', fontSize: 12, fontWeight: '600', marginBottom: 7},
  segments: {flexDirection: 'row', backgroundColor: '#111316', borderRadius: 9,
    borderWidth: 1, borderColor: '#30363A', padding: 3, marginTop: 7},
  segment: {flex: 1, alignItems: 'center', paddingVertical: 9, borderRadius: 7},
  selected: {backgroundColor: '#34433E'},
  segmentText: {color: '#9FAAAC', fontSize: 11, fontWeight: '600'},
  selectedText: {color: '#D8FFE9'},
  field: {marginTop: 12, marginBottom: 4},
  input: {backgroundColor: '#121417', borderColor: '#353C3F', borderWidth: 1,
    borderRadius: 8, color: '#E8EFED', paddingHorizontal: 11, paddingVertical: 8, fontSize: 12},
  row: {flexDirection: 'row', gap: 12}, half: {flex: 1},
  between: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center'},
  textButton: {alignSelf: 'flex-start', paddingTop: 12, paddingBottom: 4},
  link: {color: '#71D9B8', fontSize: 12, fontWeight: '600'},
  spaceTop: {marginTop: 18},
  rowCenter: {flexDirection: 'row', alignItems: 'center', gap: 12, marginTop: 4},
  step: {width: 30, height: 30, alignItems: 'center', justifyContent: 'center',
    backgroundColor: '#30363A', borderRadius: 7},
  stepText: {color: '#E9F4EE', fontSize: 18},
  track: {flex: 1, height: 5, backgroundColor: '#333B3D', borderRadius: 4},
  fill: {height: 5, backgroundColor: '#65D7B5', borderRadius: 4},
  dot: {width: 9, height: 9, borderRadius: 5, backgroundColor: '#6A6E70'},
  dotOn: {backgroundColor: '#65D7B5'},
  flex: {flex: 1, marginRight: 12},
  footnote: {color: '#8B9798', fontSize: 11, lineHeight: 17, marginTop: 4},
  empty: {flex: 1, justifyContent: 'center', alignItems: 'center', gap: 15},
  reconnect: {paddingHorizontal: 14, paddingVertical: 9, borderWidth: 1,
    borderColor: '#46534F', borderRadius: 8},
});
export default App;
