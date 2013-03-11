SubModuleLink
=============

Creates [and removes] directory junction links from nested submodules to the top-most repository.

Example
-------

Start:

Repository
  -> Submodules
    -> Foo
    -> Bar
      -> Submodules
        -> Foo

> submodulelink Repository

Repository
  -> Submodules
    -> Foo
    -> Bar
      -> Submodules
        -> Foo (junction link to Repository/Submodules/Foo)

> submodulelink /u Repository

Repository
  -> Submodules
    -> Foo
    -> Bar
      -> Submodules
        -> Foo (Empty dir)

        
Note: Submodule directories will not be modified if not empty.

  
