import { Button, Drawer } from "@heroui/react"
import { Bars } from "@gravity-ui/icons";
import { navItems, siteConfig } from "../config/site";
import { Link } from "react-router-dom";
import React from "react";
import { useI18n } from "../i18n";

export function MenuList() {
    const [isOpen, setIsOpen] = React.useState(false);
    const { t } = useI18n();

    return (
    <div className="sm:hidden">
        <Drawer isOpen={isOpen} onOpenChange={setIsOpen}>
            <Button variant="secondary" size="sm" isIconOnly>
                <Bars />
            </Button>
            <Drawer.Backdrop>
                <Drawer.Content placement="left" className="w-[60vw]">
                    <Drawer.Dialog>
                        <Drawer.CloseTrigger />
                        <Drawer.Header>
                            <Drawer.Heading>{siteConfig.name}</Drawer.Heading>
                        </Drawer.Header>
                        <Drawer.Body>
                            <nav className="flex flex-col gap-1">
                                {navItems.map((item) => (
                                <Link
                                    key={item.label}
                                    className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-foreground transition-colors hover:bg-default"
                                    to={item.url}
                                    onClick={()=>setIsOpen(false)}
                                >
                                    <item.icon className="size-5 text-muted" />
                                    {t(`nav.${item.label?.toLowerCase()}`)}
                                </Link>
                                ))}
                            </nav>
                        </Drawer.Body>
                    </Drawer.Dialog>
                </Drawer.Content>
            </Drawer.Backdrop>
        </Drawer>
    </div>
    )
}