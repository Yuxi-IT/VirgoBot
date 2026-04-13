function AcrylicBubble({ children,className }: {children: React.ReactNode, className?: string}) {
    return (
        <div className={`min-h-[48px] min-w-[48px] bg-white/40 backdrop-blur-sm shadow-md rounded-[35px] flex items-center justify-center ${className || ''}`}>
            {children}
        </div>
    )

}

export default AcrylicBubble;