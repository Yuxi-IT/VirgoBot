import { LayoutList, Comment, Persons, Gear, Rocket, ArrowShapeTurnUpRight, Clock, Database } from "@gravity-ui/icons";
import { ComponentType, SVGProps } from "react";

export const siteConfig = {
    name: "VirgoBot",
}

export const navItems: {
    icon: ComponentType<SVGProps<SVGSVGElement>>;
    label?: string;
    url: string;
    showBottomNav?: boolean;
    showInBottomNav?: boolean;
}[] = [
    { icon: LayoutList, label: "Dashboard", url: "/" },
    { icon: Comment, label: "Chat", url: "/chat", showBottomNav: false },
    { icon: Database, label: "Providers", url: "/providers", showInBottomNav: false },
    { icon: Persons, label: "Contacts", url: "/contacts", showInBottomNav: false },
    { icon: ArrowShapeTurnUpRight, label: "Channel", url: "/channel", showInBottomNav: false },
    { icon: Rocket, label: "Skills", url: "/skills", showInBottomNav: false },
    { icon: Clock, label: "Tasks", url: "/tasks", showInBottomNav: false },
    { icon: Gear, label: "Settings", url: "/settings" },
];
