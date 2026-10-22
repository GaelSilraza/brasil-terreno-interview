import { ArrowLeftRight, Wallet, type LucideIcon } from 'lucide-react';

export type NavigationUrl = '/accounts' | '/transfers';

export interface INavigationContent {
  name: string;
  url: NavigationUrl;
  icon: LucideIcon;
}

export interface INavigationGroup {
  groupName: string;
  contents: INavigationContent[];
}

export const navigationConfig: INavigationGroup[] = [
  {
    groupName: 'Operações',
    contents: [
      { name: 'Contas', url: '/accounts', icon: Wallet },
      { name: 'Transferências', url: '/transfers', icon: ArrowLeftRight },
    ],
  },
];
