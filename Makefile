
all: fup.exe

VPATH=.:./DFU:./NXPDFU

USBLIB=LibUsbDotNet.dll
USBLIBref=$(shell pkg-config --libs LibUsbDotNet)


DEFINES=-debug -define:MONO_DATACONVERTER_PUBLIC\;MONO_DATACONVERTER_STATIC_METHODS -unsafe

SOURCES=dfutypes.cs dfu.cs nxpdfutypes.cs nxpdfu.cs DataConverter.cs Options.cs

REFS=$(USBLIBref)

LIBS=$(USBLIB)

fup.exe: fup.cs $(SOURCES) $(LIBS)
	gmcs $(DEFINES) $(REFS) $(filter-out $(LIBS),$^)

LibUsbDotNet.dll:
	ln -s `pkg-config --variable=Libraries LibUsbDotNet` .

clean:
	rm -f *~ \#* LibUsbDotNet.dll *.exe
