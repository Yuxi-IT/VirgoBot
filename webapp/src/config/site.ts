import { LayoutList, Comment, Persons, Gear, FileText, Rocket, PersonWorker, CirclesIntersection, ArrowShapeTurnUpRight, Clock, Microphone, Database } from "@gravity-ui/icons";
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
    { icon: Database, label: "Providers", url: "/providers", showInBottomNav: false },
    { icon: Persons, label: "Contacts", url: "/contacts", showInBottomNav: false },
    { icon: Comment, label: "Chat", url: "/chat" },
    { icon: Microphone, label: "Voice", url: "/voice" },
    { icon: PersonWorker, label: "Agent", url: "/agent", showInBottomNav: false },
    { icon: CirclesIntersection, label: "Memory", url: "/memory" },
    { icon: ArrowShapeTurnUpRight, label: "Channel", url: "/channel", showInBottomNav: false },
    { icon: Rocket, label: "Skills", url: "/skills", showInBottomNav: false },
    { icon: Clock, label: "Tasks", url: "/tasks", showInBottomNav: false },
    { icon: FileText, label: "Logs", url: "/logs", showInBottomNav: false },
    { icon: Gear, label: "Settings", url: "/settings" },
];
