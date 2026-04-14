import { LayoutList, Comment, Persons, Gear, FileText, Rocket } from "@gravity-ui/icons";
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
    { icon: Comment, label: "Chat", url: "/chat" },
    { icon: Persons, label: "Contacts", url: "/contacts" },
    { icon: Gear, label: "Settings", url: "/settings" },
    { icon: Rocket, label: "Skills", url: "/skills", showInBottomNav: false },
    { icon: FileText, label: "Logs", url: "/logs", showInBottomNav: false },
];
