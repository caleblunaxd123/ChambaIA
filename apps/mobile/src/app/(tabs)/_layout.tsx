import { Tabs } from 'expo-router';

import { Icon, type IconName } from '@/ui/Icon';
import { fontFamily, useTheme } from '@/ui/theme';

type TabDef = { name: string; title: string; icon: IconName; focused: IconName };

const TABS: TabDef[] = [
  { name: 'index', title: 'Inicio', icon: 'home-outline', focused: 'home' },
  { name: 'jobs', title: 'Empleos', icon: 'briefcase-outline', focused: 'briefcase' },
  { name: 'applications', title: 'Postulaciones', icon: 'paper-plane-outline', focused: 'paper-plane' },
  { name: 'agent', title: 'Agente', icon: 'sparkles-outline', focused: 'sparkles' },
  { name: 'profile', title: 'Perfil', icon: 'person-outline', focused: 'person' },
];

export default function TabsLayout() {
  const { colors } = useTheme();
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textSubtle,
        tabBarLabelStyle: { fontFamily: fontFamily.semibold, fontSize: 10 },
        tabBarStyle: { backgroundColor: colors.surface, borderTopColor: colors.border, borderTopWidth: 1, paddingTop: 4 },
        sceneStyle: { backgroundColor: colors.bg },
      }}
    >
      {TABS.map((t) => (
        <Tabs.Screen
          key={t.name}
          name={t.name}
          options={{
            title: t.title,
            tabBarButtonTestID: `tab-${t.name}`,
            tabBarIcon: ({ focused, color }) => <Icon name={focused ? t.focused : t.icon} size={23} color={color} />,
          }}
        />
      ))}
    </Tabs>
  );
}
