/**
 * @format
 */

import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import App from '../App';

test('renders correctly', async () => {
  globalThis.fetch = jest.fn(() => new Promise<Response>(() => {}));
  let renderer!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    renderer = ReactTestRenderer.create(<App />);
  });
  await ReactTestRenderer.act(() => renderer.unmount());
});

test('Appearance remains renderable with fractional opacity and exposes photo controls', async () => {
  globalThis.fetch = jest.fn(async () => ({ok: true, json: async () => ({
    surfaceOpacity: 0.42, edge: 0, alignment: 1, horizontalOffset: 0,
    verticalOffset: 0, panelWidth: 650, panelHeight: 400,
    appPanelWidth: 920, appPanelHeight: 600, theme: 0,
    surfaceStyle: 0, accentColor: '#FF6ED6A9', backgroundColor: '#FF111113', gradientStartColor: '#FF334654',
    gradientEndColor: '#FF161821', gradientDirection: 2, logoStyle: 4,
    useAlbumArtBackdrop: true, useMediaArtworkColors: true, useAudioReactiveEffects: true,
    tempVaultRoot: 'D:\\TempVault\\storage', tempVaultAutoCopy: false,
    tempVaultConnected: false, notificationsSupported: false,
    notificationsEnabled: false, notificationStatus: 'Requires MSIX',
    hasCustomMiniLogo: false,
  })}) as Response);
  let renderer!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => { renderer = ReactTestRenderer.create(<App />); });
  const appearance = renderer.root.findAll(node => node.props?.children === 'Appearance');
  let clickable: ReactTestRenderer.ReactTestInstance | null = appearance[0];
  while (clickable && typeof clickable.props.onPress !== 'function') clickable = clickable.parent;
  expect(clickable).not.toBeNull();
  await ReactTestRenderer.act(() => clickable?.props.onPress());
  expect(renderer.root.findAll(node => node.props?.children === 'Choose and crop picture').length).toBeGreaterThan(0);
  expect(renderer.root.findAll(node => node.props?.children === 'Audio-reactive waves').length).toBeGreaterThan(0);
  expect(renderer.root.findAll(node => node.props?.children === 'This screen could not render').length).toBe(0);
  await ReactTestRenderer.act(() => renderer.unmount());
});
